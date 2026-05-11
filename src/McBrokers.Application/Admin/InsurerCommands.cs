using McBrokers.Domain.Insurers;

namespace McBrokers.Application.Admin;

public sealed record CreateInsurerCommand(InsurerCode Code, string Name, int DisplayOrder);

public sealed record UpdateInsurerCommand(
    Guid Id,
    string Name,
    int DisplayOrder,
    bool IsEnabled,
    string? LogoUrl);

public sealed record UpsertInsurerConfigCommand(
    Guid InsurerId,
    InsurerEnvironment Environment,
    string EndpointUrl,
    string BusinessNumber,
    string AgentCode,
    string KeyVaultSecretName,
    int TimeoutSeconds,
    int MaxRetries);
