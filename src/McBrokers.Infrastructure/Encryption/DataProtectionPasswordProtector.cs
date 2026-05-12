using McBrokers.Application.Ports;
using Microsoft.AspNetCore.DataProtection;

namespace McBrokers.Infrastructure.Encryption;

/// <summary>
/// Implementación productiva de IPasswordProtector usando ASP.NET Core DataProtection.
/// El keyring persiste localmente en %LOCALAPPDATA%\ASP.NET\DataProtection-Keys; en Azure
/// App Service va a Blob Storage configurable via PersistKeysToAzureBlobStorage.
///
/// Propósito específico "McBrokers.Insurers.Passwords.v1" — versionar el propósito permite
/// rotar el método de cifrado en el futuro sin invalidar valores antiguos.
/// </summary>
public sealed class DataProtectionPasswordProtector : IPasswordProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionPasswordProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("McBrokers.Insurers.Passwords.v1");
    }

    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return string.Empty;
        return _protector.Protect(plaintext);
    }

    public string Unprotect(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext)) return string.Empty;
        return _protector.Unprotect(ciphertext);
    }
}
