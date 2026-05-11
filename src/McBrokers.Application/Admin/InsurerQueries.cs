using McBrokers.Application.Ports;
using McBrokers.Domain.Insurers;

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

    public GetInsurer(IInsurerRepository insurers, IInsurerConfigRepository configs)
    {
        _insurers = insurers;
        _configs = configs;
    }

    public async Task<InsurerDetailView?> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var insurer = await _insurers.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (insurer is null) return null;

        var configs = await _configs.ListByInsurerAsync(id, cancellationToken).ConfigureAwait(false);
        return InsurerDetailView.From(insurer, configs);
    }
}

public sealed record InsurerConfigView(
    Guid Id,
    InsurerEnvironment Environment,
    string EndpointUrl,
    string BusinessNumber,
    string AgentCode,
    string KeyVaultSecretName,
    int TimeoutSeconds,
    int MaxRetries)
{
    public static InsurerConfigView From(InsurerConfig cfg) => new(
        cfg.Id, cfg.Environment, cfg.EndpointUrl, cfg.BusinessNumber,
        cfg.AgentCode, cfg.KeyVaultSecretName, cfg.TimeoutSeconds, cfg.MaxRetries);
}

public sealed record InsurerDetailView(InsurerView Insurer, IReadOnlyList<InsurerConfigView> Configs)
{
    public static InsurerDetailView From(Insurer insurer, IReadOnlyList<InsurerConfig> configs) =>
        new(InsurerView.From(insurer), configs.Select(InsurerConfigView.From).ToList());
}
