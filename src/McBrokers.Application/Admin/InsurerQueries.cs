using McBrokers.Application.Ports;
using McBrokers.Domain.Insurers;
using McBrokers.Domain.Quotations;

namespace McBrokers.Application.Admin;

public sealed record InsurerView(
    Guid Id,
    InsurerCode Code,
    string Name,
    bool IsEnabled,
    int DisplayOrder,
    string? LogoUrl)
{
    public static InsurerView From(Insurer insurer) =>
        new(insurer.Id, insurer.Code, insurer.Name, insurer.IsEnabled, insurer.DisplayOrder, insurer.LogoUrl);
}

public sealed class ListInsurers
{
    private readonly IInsurerRepository _insurers;

    public ListInsurers(IInsurerRepository insurers) => _insurers = insurers;

    public async Task<IReadOnlyList<InsurerView>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var insurers = await _insurers.ListAsync(cancellationToken).ConfigureAwait(false);
        return insurers
            .OrderBy(i => i.DisplayOrder)
            .ThenBy(i => i.Name)
            .Select(InsurerView.From)
            .ToList();
    }
}

public sealed class GetInsurer
{
    private readonly IInsurerRepository _insurers;
    private readonly IInsurerConfigRepository _configs;
    private readonly IInsurerPackageMappingRepository _packageMappings;

    public GetInsurer(
        IInsurerRepository insurers,
        IInsurerConfigRepository configs,
        IInsurerPackageMappingRepository packageMappings)
    {
        _insurers = insurers;
        _configs = configs;
        _packageMappings = packageMappings;
    }

    public async Task<InsurerDetailView?> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var insurer = await _insurers.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (insurer is null) return null;

        var config = await _configs.GetAsync(id, cancellationToken).ConfigureAwait(false);
        var packageMappings = await _packageMappings.ListByInsurerAsync(id, cancellationToken).ConfigureAwait(false);
        return InsurerDetailView.From(insurer, config, packageMappings);
    }
}

public sealed record InsurerConfigView(
    Guid Id,
    string EndpointUrl,
    string BusinessNumber,
    string AgentCode,
    string KeyVaultSecretName,
    int TimeoutSeconds,
    int MaxRetries)
{
    public static InsurerConfigView From(InsurerConfig cfg) => new(
        cfg.Id, cfg.EndpointUrl, cfg.BusinessNumber,
        cfg.AgentCode, cfg.KeyVaultSecretName, cfg.TimeoutSeconds, cfg.MaxRetries);
}

public sealed record InsurerPackageMappingView(
    Guid Id,
    PackageCode InternalPackage,
    string ExternalCode,
    string? Description)
{
    public static InsurerPackageMappingView From(InsurerPackageMapping m) =>
        new(m.Id, m.InternalPackage, m.ExternalCode, m.Description);
}

public sealed record InsurerDetailView(
    InsurerView Insurer,
    InsurerConfigView? Config,
    IReadOnlyList<InsurerPackageMappingView> PackageMappings)
{
    public static InsurerDetailView From(
        Insurer insurer,
        InsurerConfig? config,
        IReadOnlyList<InsurerPackageMapping> packageMappings) =>
        new(InsurerView.From(insurer),
            config is null ? null : InsurerConfigView.From(config),
            packageMappings.Select(InsurerPackageMappingView.From).ToList());
}
