using McBrokers.SharedKernel;

namespace McBrokers.Domain.Insurers;

public sealed class InsurerConfig
{
    private const int MaxTimeoutSeconds = 600;

    public Guid Id { get; }
    public Guid InsurerId { get; }
    public InsurerEnvironment Environment { get; }
    public string EndpointUrl { get; private set; }
    public string BusinessNumber { get; private set; }
    public string AgentCode { get; private set; }
    public string KeyVaultSecretName { get; private set; }
    public int TimeoutSeconds { get; private set; }
    public int MaxRetries { get; private set; }

    private InsurerConfig(
        Guid id,
        Guid insurerId,
        InsurerEnvironment environment,
        string endpointUrl,
        string businessNumber,
        string agentCode,
        string keyVaultSecretName,
        int timeoutSeconds,
        int maxRetries)
    {
        Id = id;
        InsurerId = insurerId;
        Environment = environment;
        EndpointUrl = endpointUrl;
        BusinessNumber = businessNumber;
        AgentCode = agentCode;
        KeyVaultSecretName = keyVaultSecretName;
        TimeoutSeconds = timeoutSeconds;
        MaxRetries = maxRetries;
    }

    public static Result<InsurerConfig> Create(
        Guid insurerId,
        InsurerEnvironment environment,
        string endpointUrl,
        string businessNumber,
        string agentCode,
        string keyVaultSecretName,
        int timeoutSeconds,
        int maxRetries)
    {
        var validation = Validate(endpointUrl, businessNumber, agentCode, keyVaultSecretName, timeoutSeconds, maxRetries);
        if (!validation.IsSuccess)
        {
            return Result<InsurerConfig>.Failure(validation.Error);
        }

        return Result<InsurerConfig>.Success(new InsurerConfig(
            Guid.NewGuid(),
            insurerId,
            environment,
            endpointUrl,
            businessNumber.Trim(),
            agentCode.Trim(),
            keyVaultSecretName.Trim(),
            timeoutSeconds,
            maxRetries));
    }

    public Result<InsurerConfig> Update(
        string endpointUrl,
        string businessNumber,
        string agentCode,
        string keyVaultSecretName,
        int timeoutSeconds,
        int maxRetries)
    {
        var validation = Validate(endpointUrl, businessNumber, agentCode, keyVaultSecretName, timeoutSeconds, maxRetries);
        if (!validation.IsSuccess)
        {
            return Result<InsurerConfig>.Failure(validation.Error);
        }

        EndpointUrl = endpointUrl;
        BusinessNumber = businessNumber.Trim();
        AgentCode = agentCode.Trim();
        KeyVaultSecretName = keyVaultSecretName.Trim();
        TimeoutSeconds = timeoutSeconds;
        MaxRetries = maxRetries;

        return Result<InsurerConfig>.Success(this);
    }

    private static Result<bool> Validate(
        string endpointUrl,
        string businessNumber,
        string agentCode,
        string keyVaultSecretName,
        int timeoutSeconds,
        int maxRetries)
    {
        if (string.IsNullOrWhiteSpace(endpointUrl)
            || !Uri.TryCreate(endpointUrl, UriKind.Absolute, out var parsed)
            || parsed.Scheme != Uri.UriSchemeHttps)
        {
            return Result<bool>.Failure("Endpoint URL must be an absolute https URL.");
        }

        if (string.IsNullOrWhiteSpace(businessNumber))
        {
            return Result<bool>.Failure("BusinessNumber must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(agentCode))
        {
            return Result<bool>.Failure("AgentCode must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(keyVaultSecretName))
        {
            return Result<bool>.Failure("KeyVaultSecretName (the key vault entry holding credentials) must not be empty.");
        }

        if (timeoutSeconds <= 0 || timeoutSeconds > MaxTimeoutSeconds)
        {
            return Result<bool>.Failure($"TimeoutSeconds must be between 1 and {MaxTimeoutSeconds}.");
        }

        if (maxRetries < 0)
        {
            return Result<bool>.Failure("MaxRetries must be zero or positive.");
        }

        return Result<bool>.Success(true);
    }
}
