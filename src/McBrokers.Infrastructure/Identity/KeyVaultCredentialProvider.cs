using McBrokers.Application.Quotations;
using Microsoft.Extensions.Configuration;

namespace McBrokers.Infrastructure.Identity;

/// <summary>
/// F3 placeholder: lee credenciales desde IConfiguration usando el secret name como prefijo
/// (ej. "Insurers__Gnp__credentials" → busca Insurers:Gnp:credentials:Username y :Password).
/// En producción, IConfiguration mismo apuntará a Key Vault — la implementación se mantiene.
/// </summary>
public sealed class KeyVaultCredentialProvider : IInsurerCredentialProvider
{
    private readonly IConfiguration _config;

    public KeyVaultCredentialProvider(IConfiguration config) => _config = config;

    public Task<InsurerCredentialPair> ResolveAsync(string keyVaultSecretName, CancellationToken cancellationToken)
    {
        // Convertir "Insurers--Gnp--credentials" (estilo Key Vault) a "Insurers:Gnp:credentials" para IConfiguration.
        var path = keyVaultSecretName.Replace("--", ":", StringComparison.Ordinal);
        var username = _config[$"{path}:Username"] ?? string.Empty;
        var password = _config[$"{path}:Password"] ?? string.Empty;
        return Task.FromResult(new InsurerCredentialPair(username, password));
    }
}
