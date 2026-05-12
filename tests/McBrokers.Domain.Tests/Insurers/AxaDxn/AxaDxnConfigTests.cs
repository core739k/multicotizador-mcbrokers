using McBrokers.Domain.Insurers.AxaDxn;

namespace McBrokers.Domain.Tests.Insurers.AxaDxn;

public class AxaDxnConfigTests
{
    private static readonly Guid AnInsurerId = Guid.NewGuid();

    private static AxaDxnConfig Build() => AxaDxnConfig.Create(
        AnInsurerId,
        usuario: "MCBROKERS",
        password: "secret",
        tarifa: "RES",
        tarifaPickup: "PCK",
        descuento: 15,
        descuentoPickup: 20,
        mesPolizaDefault: 5,
        copsisD4Key: "d4-abc-123",
        copsisB: "b-xyz-456").Value;

    [Fact]
    public void Create_with_valid_inputs_succeeds()
    {
        var result = AxaDxnConfig.Create(
            AnInsurerId,
            usuario: "MCBROKERS",
            password: "secret",
            tarifa: "RES",
            tarifaPickup: "PCK",
            descuento: 15,
            descuentoPickup: 20,
            mesPolizaDefault: 5,
            copsisD4Key: "d4-abc-123",
            copsisB: "b-xyz-456");

        result.IsSuccess.Should().BeTrue();
        var cfg = result.Value;
        cfg.InsurerId.Should().Be(AnInsurerId);
        cfg.Usuario.Should().Be("MCBROKERS");
        cfg.Password.Should().Be("secret");
        cfg.Tarifa.Should().Be("RES");
        cfg.TarifaPickup.Should().Be("PCK");
        cfg.Descuento.Should().Be(15);
        cfg.DescuentoPickup.Should().Be(20);
        cfg.MesPolizaDefault.Should().Be(5);
        cfg.CopsisD4Key.Should().Be("d4-abc-123");
        cfg.CopsisB.Should().Be("b-xyz-456");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_usuario(string? usuario)
    {
        var result = AxaDxnConfig.Create(AnInsurerId, usuario!, "p", "RES", "PCK", 0, 0, 1, "d4", "b");
        result.IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_password(string? password)
    {
        var result = AxaDxnConfig.Create(AnInsurerId, "u", password!, "RES", "PCK", 0, 0, 1, "d4", "b");
        result.IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_copsis_d4key(string? d4key)
    {
        var result = AxaDxnConfig.Create(AnInsurerId, "u", "p", "RES", "PCK", 0, 0, 1, d4key!, "b");
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("CopsisD4Key",
            because: "el mensaje debe nombrar el campo concreto");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_copsis_b(string? b)
    {
        var result = AxaDxnConfig.Create(AnInsurerId, "u", "p", "RES", "PCK", 0, 0, 1, "d4", b!);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("CopsisB");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void Create_rejects_out_of_range_descuento(int descuento)
    {
        var result = AxaDxnConfig.Create(AnInsurerId, "u", "p", "RES", "PCK", descuento, 0, 1, "d4", "b");
        result.IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Create_rejects_out_of_range_mes(int mes)
    {
        var result = AxaDxnConfig.Create(AnInsurerId, "u", "p", "RES", "PCK", 0, 0, mes, "d4", "b");
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Update_changes_all_mutable_fields()
    {
        var cfg = Build();

        var result = cfg.Update(
            usuario: "new",
            password: "new-pwd",
            tarifa: "NEW",
            tarifaPickup: "NEW-P",
            descuento: 30,
            descuentoPickup: 25,
            mesPolizaDefault: 12,
            copsisD4Key: "d4-new",
            copsisB: "b-new");

        result.IsSuccess.Should().BeTrue();
        cfg.Usuario.Should().Be("new");
        cfg.Password.Should().Be("new-pwd");
        cfg.Tarifa.Should().Be("NEW");
        cfg.TarifaPickup.Should().Be("NEW-P");
        cfg.Descuento.Should().Be(30);
        cfg.DescuentoPickup.Should().Be(25);
        cfg.MesPolizaDefault.Should().Be(12);
        cfg.CopsisD4Key.Should().Be("d4-new");
        cfg.CopsisB.Should().Be("b-new");
    }

    [Fact]
    public void Update_keeps_previous_state_when_invalid()
    {
        var cfg = Build();

        var result = cfg.Update("u", "p", "RES", "PCK", 0, 0, 99, "d4", "b");

        result.IsSuccess.Should().BeFalse();
        cfg.MesPolizaDefault.Should().Be(5, because: "no field changes if any field is invalid");
        cfg.CopsisD4Key.Should().Be("d4-abc-123",
            because: "Update con un campo inválido no debe mutar ningún otro campo");
    }
}
