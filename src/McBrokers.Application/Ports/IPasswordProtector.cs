namespace McBrokers.Application.Ports;

/// <summary>
/// Cifra/descifra strings sensibles (passwords de aseguradoras) para almacenamiento
/// columna-a-columna en BD. La implementación productiva usa ASP.NET Core DataProtection;
/// en tests se usa NullPasswordProtector (passthrough).
/// </summary>
public interface IPasswordProtector
{
    string Protect(string plaintext);

    string Unprotect(string ciphertext);
}
