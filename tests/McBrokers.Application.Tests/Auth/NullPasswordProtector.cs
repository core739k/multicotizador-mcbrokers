using McBrokers.Application.Ports;

namespace McBrokers.Application.Tests.Auth;

/// <summary>
/// Test double: passthrough sin cifrar. Permite que los tests unitarios verifiquen
/// la lógica de negocio sin acoplar a ASP.NET Core DataProtection.
/// </summary>
public sealed class NullPasswordProtector : IPasswordProtector
{
    public string Protect(string plaintext) => plaintext;
    public string Unprotect(string ciphertext) => ciphertext;
}
