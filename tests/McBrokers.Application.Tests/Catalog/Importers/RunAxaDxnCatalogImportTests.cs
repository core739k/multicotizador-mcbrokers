using McBrokers.Application.Catalog;
using McBrokers.Application.Catalog.Importers;
using McBrokers.Application.Ports;
using McBrokers.Domain.Insurers;
using McBrokers.Domain.Insurers.AxaDxn;
using McBrokers.SharedKernel;

namespace McBrokers.Application.Tests.Catalog.Importers;

/// <summary>
/// Orquestador de importación de catálogo AXA DXN: dispara 4 llamadas SOAP
/// (Marca + Submarca por cada tarifa), filtra idTipoVehiculo excluidos,
/// expande filas por año dentro de [currentYear-1, currentYear] y delega
/// la persistencia a IImportInsurerCatalog con IsSourceOfTruth=true.
/// </summary>
public class RunAxaDxnCatalogImportTests
{
    private static readonly DateTime Now = new(2026, 5, 18, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid InsurerId = Guid.NewGuid();

    private readonly Mock<IInsurerRepository> _insurers = new(MockBehavior.Strict);
    private readonly Mock<IAxaDxnConfigRepository> _axaConfigs = new(MockBehavior.Strict);
    private readonly Mock<IInsurerConfigRepository> _insurerConfigs = new(MockBehavior.Strict);
    private readonly Mock<IImportInsurerCatalog> _importer = new(MockBehavior.Strict);
    private readonly Mock<IClock> _clock = new(MockBehavior.Strict);
    private readonly FakeAxaDxnCatalogClient _client = new();

    public RunAxaDxnCatalogImportTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
    }

    private RunAxaDxnCatalogImport Build() =>
        new(_insurers.Object, _axaConfigs.Object, _insurerConfigs.Object,
            _client, _clock.Object, _importer.Object);

    private void SetupHappyPath_NoInsurerConfig()
    {
        var axa = Insurer.Create(InsurerCode.AxaDxn, "AXA DXN", displayOrder: 0).Value;
        _insurers.Setup(r => r.GetByIdAsync(InsurerId, It.IsAny<CancellationToken>())).ReturnsAsync(axa);

        var cfg = AxaDxnConfig.Create(
            InsurerId,
            usuario: "USER01",
            password: "secret",
            tarifa: "TAR-AUTOS",
            tarifaPickup: "TAR-PICKUP",
            descuento: 0,
            descuentoPickup: 0,
            mesPolizaDefault: 5,
            copsisD4Key: "d4",
            copsisB: "b").Value;
        _axaConfigs.Setup(r => r.GetByInsurerIdAsync(InsurerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AxaDxnConfigWithBusinesses(cfg, Array.Empty<AxaDxnBusiness>()));

        _insurerConfigs.Setup(r => r.GetAsync(InsurerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InsurerConfig?)null);
    }

    private void SetupImporterEcho()
    {
        _importer.Setup(i => i.ExecuteAsync(
                It.IsAny<ImportInsurerCatalogCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ImportInsurerCatalogCommand cmd, CancellationToken _) =>
                Result<ImportInsurerCatalogResult>.Success(
                    new ImportInsurerCatalogResult(Guid.NewGuid(), cmd.Rows.Count, cmd.Rows.Count, 0, 0)));
    }

    [Fact]
    public async Task Happy_path_invokes_client_four_times_in_marca_then_submarca_per_tarifa_order()
    {
        SetupHappyPath_NoInsurerConfig();
        SetupImporterEcho();
        _client.SetEmptyResponseForAll();

        var result = await Build().ExecuteAsync(InsurerId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        _client.Calls.Should().HaveCount(4);
        _client.Calls[0].Should().Be(("TAR-AUTOS", "Marca"));
        _client.Calls[1].Should().Be(("TAR-AUTOS", "Submarca"));
        _client.Calls[2].Should().Be(("TAR-PICKUP", "Marca"));
        _client.Calls[3].Should().Be(("TAR-PICKUP", "Submarca"));
    }

    [Fact]
    public async Task Insurer_not_found_returns_failure_and_skips_all_io()
    {
        _insurers.Setup(r => r.GetByIdAsync(InsurerId, It.IsAny<CancellationToken>())).ReturnsAsync((Insurer?)null);

        var result = await Build().ExecuteAsync(InsurerId, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("INSURER_NOT_FOUND");
        _client.Calls.Should().BeEmpty();
        _importer.Verify(i => i.ExecuteAsync(It.IsAny<ImportInsurerCatalogCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Wrong_insurer_code_returns_failure()
    {
        var gnp = Insurer.Create(InsurerCode.Gnp, "GNP", displayOrder: 0).Value;
        _insurers.Setup(r => r.GetByIdAsync(InsurerId, It.IsAny<CancellationToken>())).ReturnsAsync(gnp);

        var result = await Build().ExecuteAsync(InsurerId, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("INVALID_INSURER_FOR_AXA_DXN");
        _client.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Missing_axa_config_returns_failure_and_skips_client_call()
    {
        var axa = Insurer.Create(InsurerCode.AxaDxn, "AXA DXN", displayOrder: 0).Value;
        _insurers.Setup(r => r.GetByIdAsync(InsurerId, It.IsAny<CancellationToken>())).ReturnsAsync(axa);
        _axaConfigs.Setup(r => r.GetByInsurerIdAsync(InsurerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AxaDxnConfigWithBusinesses?)null);

        var result = await Build().ExecuteAsync(InsurerId, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("AXA_DXN_CONFIG_MISSING");
        _client.Calls.Should().BeEmpty();
        _importer.Verify(i => i.ExecuteAsync(It.IsAny<ImportInsurerCatalogCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Client_failure_propagates_and_stops_pipeline_without_invoking_importer()
    {
        SetupHappyPath_NoInsurerConfig();
        // First call (TAR-AUTOS, Marca) succeeds. Second call (TAR-AUTOS, Submarca) fails.
        _client.Queue(Result<IReadOnlyList<AxaDxnCatalogRecord>>.Success(Array.Empty<AxaDxnCatalogRecord>()));
        _client.Queue(Result<IReadOnlyList<AxaDxnCatalogRecord>>.Failure("HTTP_500"));

        var result = await Build().ExecuteAsync(InsurerId, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("AXA_DXN_FETCH_FAILED");
        result.Error.Should().Contain("TAR-AUTOS");
        result.Error.Should().Contain("Submarca");
        _client.Calls.Should().HaveCount(2, "pipeline stops at first failure");
        _importer.Verify(i => i.ExecuteAsync(It.IsAny<ImportInsurerCatalogCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Rows_passed_to_importer_have_brand_resolved_from_marca_catalog()
    {
        SetupHappyPath_NoInsurerConfig();
        SetupImporterEcho();

        _client.Queue(SingleMarca(idMarca: "42", descripcion: "TOYOTA"));
        _client.Queue(SingleSubmarca(idMarca: "42", descripcion: "COROLLA SE", claveAmis: "01234", from: 2024, to: 2027));
        _client.QueueEmpty();
        _client.QueueEmpty();

        await Build().ExecuteAsync(InsurerId, CancellationToken.None);

        var cmd = _client.CapturedImportCommand(_importer);
        cmd.Rows.Should().NotBeEmpty();
        cmd.Rows.Should().OnlyContain(r => r.Brand == "TOYOTA");
    }

    [Fact]
    public async Task Submarca_with_brand_not_in_marca_catalog_is_dropped()
    {
        SetupHappyPath_NoInsurerConfig();
        SetupImporterEcho();

        _client.Queue(SingleMarca(idMarca: "42", descripcion: "TOYOTA"));
        // Submarca references idMarca=99 which is not in the marca catalog.
        _client.Queue(SingleSubmarca(idMarca: "99", descripcion: "PHANTOM X", claveAmis: "99999", from: 2025, to: 2026));
        _client.QueueEmpty();
        _client.QueueEmpty();

        await Build().ExecuteAsync(InsurerId, CancellationToken.None);

        var cmd = _client.CapturedImportCommand(_importer);
        cmd.Rows.Should().BeEmpty();
    }

    [Theory]
    [InlineData("22")]
    [InlineData("3")]
    [InlineData("81")]
    [InlineData("7")]
    [InlineData("24")]
    public async Task Excluded_idTipoVehiculo_in_submarca_is_filtered_out(string excludedType)
    {
        SetupHappyPath_NoInsurerConfig();
        SetupImporterEcho();

        _client.Queue(SingleMarca(idMarca: "42", descripcion: "TOYOTA"));
        _client.Queue(new[]
        {
            new AxaDxnCatalogRecord("42", excludedType, "COROLLA SE", "5", "01234", 2024, 2027),
        });
        _client.QueueEmpty();
        _client.QueueEmpty();

        await Build().ExecuteAsync(InsurerId, CancellationToken.None);

        var cmd = _client.CapturedImportCommand(_importer);
        cmd.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task Marca_with_excluded_idTipoVehiculo_drops_all_its_submarcas()
    {
        SetupHappyPath_NoInsurerConfig();
        SetupImporterEcho();

        // Marca 42 only appears with idTipoVehiculo=22 (excluded) → brand lookup fails.
        _client.Queue(new[] { new AxaDxnCatalogRecord("42", "22", "TOYOTA", null, null, null, null) });
        _client.Queue(SingleSubmarca(idMarca: "42", descripcion: "COROLLA SE", claveAmis: "01234", from: 2025, to: 2026));
        _client.QueueEmpty();
        _client.QueueEmpty();

        await Build().ExecuteAsync(InsurerId, CancellationToken.None);

        var cmd = _client.CapturedImportCommand(_importer);
        cmd.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task Submarca_is_expanded_one_row_per_year_within_current_year_minus_one_to_current_year()
    {
        SetupHappyPath_NoInsurerConfig();
        SetupImporterEcho();

        _client.Queue(SingleMarca(idMarca: "42", descripcion: "TOYOTA"));
        // Range 2023..2028 ∩ {2025, 2026} = {2025, 2026}
        _client.Queue(SingleSubmarca(idMarca: "42", descripcion: "COROLLA SE 1.8L", claveAmis: "01234", from: 2023, to: 2028));
        _client.QueueEmpty();
        _client.QueueEmpty();

        await Build().ExecuteAsync(InsurerId, CancellationToken.None);

        var cmd = _client.CapturedImportCommand(_importer);
        cmd.Rows.Select(r => r.Year).Should().BeEquivalentTo(new[] { 2025, 2026 });
    }

    [Fact]
    public async Task Submarca_with_range_fully_outside_year_window_yields_no_rows()
    {
        SetupHappyPath_NoInsurerConfig();
        SetupImporterEcho();

        _client.Queue(SingleMarca(idMarca: "42", descripcion: "TOYOTA"));
        // Range 2010..2020 ∩ {2025, 2026} = ∅
        _client.Queue(SingleSubmarca(idMarca: "42", descripcion: "COROLLA SE", claveAmis: "01234", from: 2010, to: 2020));
        _client.QueueEmpty();
        _client.QueueEmpty();

        await Build().ExecuteAsync(InsurerId, CancellationToken.None);

        _client.CapturedImportCommand(_importer).Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task Submarca_with_range_starting_in_future_year_yields_only_overlap()
    {
        SetupHappyPath_NoInsurerConfig();
        SetupImporterEcho();

        _client.Queue(SingleMarca(idMarca: "42", descripcion: "TOYOTA"));
        // Range 2026..2030 ∩ {2025, 2026} = {2026}
        _client.Queue(SingleSubmarca(idMarca: "42", descripcion: "COROLLA SE", claveAmis: "01234", from: 2026, to: 2030));
        _client.QueueEmpty();
        _client.QueueEmpty();

        await Build().ExecuteAsync(InsurerId, CancellationToken.None);

        var rows = _client.CapturedImportCommand(_importer).Rows;
        rows.Select(r => r.Year).Should().BeEquivalentTo(new[] { 2026 });
    }

    [Fact]
    public async Task Model_is_first_word_of_descripcion_and_version_is_full_descripcion()
    {
        SetupHappyPath_NoInsurerConfig();
        SetupImporterEcho();

        _client.Queue(SingleMarca(idMarca: "42", descripcion: "TOYOTA"));
        _client.Queue(SingleSubmarca(idMarca: "42", descripcion: "COROLLA SE 1.8L AT", claveAmis: "01234", from: 2025, to: 2026));
        _client.QueueEmpty();
        _client.QueueEmpty();

        await Build().ExecuteAsync(InsurerId, CancellationToken.None);

        var row = _client.CapturedImportCommand(_importer).Rows[0];
        row.Model.Should().Be("COROLLA");
        row.Version.Should().Be("COROLLA SE 1.8L AT");
    }

    [Fact]
    public async Task External_clave_contains_claveAmis_and_year_to_satisfy_unique_constraint()
    {
        SetupHappyPath_NoInsurerConfig();
        SetupImporterEcho();

        _client.Queue(SingleMarca(idMarca: "42", descripcion: "TOYOTA"));
        _client.Queue(SingleSubmarca(idMarca: "42", descripcion: "COROLLA SE", claveAmis: "01234", from: 2025, to: 2026));
        _client.QueueEmpty();
        _client.QueueEmpty();

        await Build().ExecuteAsync(InsurerId, CancellationToken.None);

        var rows = _client.CapturedImportCommand(_importer).Rows;
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.ExternalClave.Contains("01234"));
        rows.Select(r => r.ExternalClave).Distinct().Should().HaveCount(2, "each year must have a unique ExternalClave");
    }

    [Fact]
    public async Task Importer_is_invoked_with_isSourceOfTruth_true_and_source_AxaDxn_WS()
    {
        SetupHappyPath_NoInsurerConfig();
        SetupImporterEcho();
        _client.SetEmptyResponseForAll();

        await Build().ExecuteAsync(InsurerId, CancellationToken.None);

        var cmd = _client.CapturedImportCommand(_importer);
        cmd.IsSourceOfTruth.Should().BeTrue();
        cmd.Source.Should().Be("AxaDxn-WS");
        cmd.InsurerId.Should().Be(InsurerId);
    }

    [Fact]
    public async Task Endpoint_url_from_insurer_config_overrides_hardcoded_default()
    {
        var axa = Insurer.Create(InsurerCode.AxaDxn, "AXA DXN", displayOrder: 0).Value;
        _insurers.Setup(r => r.GetByIdAsync(InsurerId, It.IsAny<CancellationToken>())).ReturnsAsync(axa);

        var cfg = AxaDxnConfig.Create(InsurerId, "USER", "secret", "TAR-AUTOS", "TAR-PICKUP", 0, 0, 5, "d4", "b").Value;
        _axaConfigs.Setup(r => r.GetByInsurerIdAsync(InsurerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AxaDxnConfigWithBusinesses(cfg, Array.Empty<AxaDxnBusiness>()));

        var custom = InsurerConfig.Create(InsurerId,
            endpointUrl: "https://custom.axa.example/EmisionPolizasWS/services/SolicitudPolizasService",
            businessNumber: "B1", agentCode: "A1", keyVaultSecretName: "k", timeoutSeconds: 30, maxRetries: 1).Value;
        _insurerConfigs.Setup(r => r.GetAsync(InsurerId, It.IsAny<CancellationToken>())).ReturnsAsync(custom);

        SetupImporterEcho();
        _client.SetEmptyResponseForAll();

        await Build().ExecuteAsync(InsurerId, CancellationToken.None);

        _client.CapturedCredentials.Should().NotBeNull();
        _client.CapturedCredentials!.EndpointUrl.Should().Be("https://custom.axa.example/EmisionPolizasWS/services/SolicitudPolizasService");
    }

    [Fact]
    public async Task Endpoint_url_falls_back_to_hardcoded_default_when_insurer_config_missing()
    {
        SetupHappyPath_NoInsurerConfig();
        SetupImporterEcho();
        _client.SetEmptyResponseForAll();

        await Build().ExecuteAsync(InsurerId, CancellationToken.None);

        _client.CapturedCredentials!.EndpointUrl
            .Should().Be("https://serviciosweb.axa.com.mx:9104/EmisionPolizasWS/services/SolicitudPolizasService");
    }

    // ---------- helpers ----------

    private static IReadOnlyList<AxaDxnCatalogRecord> SingleMarca(string idMarca, string descripcion) =>
        new[] { new AxaDxnCatalogRecord(idMarca, "1", descripcion, null, null, null, null) };

    private static IReadOnlyList<AxaDxnCatalogRecord> SingleSubmarca(
        string idMarca, string descripcion, string claveAmis, int from, int to) =>
        new[] { new AxaDxnCatalogRecord(idMarca, "2", descripcion, "5", claveAmis, from, to) };

    /// <summary>
    /// Stub del cliente que (a) registra cada llamada con (tarifa, nombreCatalogo)
    /// y (b) devuelve las respuestas en orden FIFO desde una cola; si la cola se
    /// vacía, devuelve lista vacía. Captura las credenciales para inspección.
    /// </summary>
    private sealed class FakeAxaDxnCatalogClient : IAxaDxnCatalogClient
    {
        private readonly Queue<Result<IReadOnlyList<AxaDxnCatalogRecord>>> _responses = new();
        public List<(string Tarifa, string NombreCatalogo)> Calls { get; } = new();
        public AxaDxnCatalogCredentials? CapturedCredentials { get; private set; }

        public void Queue(Result<IReadOnlyList<AxaDxnCatalogRecord>> response) => _responses.Enqueue(response);

        public void Queue(IReadOnlyList<AxaDxnCatalogRecord> records) =>
            _responses.Enqueue(Result<IReadOnlyList<AxaDxnCatalogRecord>>.Success(records));

        public void QueueEmpty() => Queue(Array.Empty<AxaDxnCatalogRecord>());

        public void SetEmptyResponseForAll()
        {
            // Pre-fill 4 empty responses for orchestrator's full pipeline.
            for (var i = 0; i < 4; i++) QueueEmpty();
        }

        public Task<Result<IReadOnlyList<AxaDxnCatalogRecord>>> FetchAsync(
            AxaDxnCatalogCredentials credentials, string tarifa, string nombreCatalogo, CancellationToken ct)
        {
            CapturedCredentials = credentials;
            Calls.Add((tarifa, nombreCatalogo));
            var response = _responses.Count > 0
                ? _responses.Dequeue()
                : Result<IReadOnlyList<AxaDxnCatalogRecord>>.Success(Array.Empty<AxaDxnCatalogRecord>());
            return Task.FromResult(response);
        }

        public ImportInsurerCatalogCommand CapturedImportCommand(Mock<IImportInsurerCatalog> mock)
        {
            ImportInsurerCatalogCommand? captured = null;
            mock.Invocations.Should().ContainSingle(i => i.Method.Name == nameof(IImportInsurerCatalog.ExecuteAsync));
            captured = (ImportInsurerCatalogCommand)mock.Invocations
                .First(i => i.Method.Name == nameof(IImportInsurerCatalog.ExecuteAsync))
                .Arguments[0];
            return captured!;
        }
    }
}
