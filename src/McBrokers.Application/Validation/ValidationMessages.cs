namespace McBrokers.Application.Validation;

// Mensajes de validación en español usados por los DataAnnotations de los
// InputModels en /Cotizacion y /Emision. Centralizados aquí para que (a) los
// tests verifiquen que están en es-MX y (b) la traducción a otra plaza sea
// un solo punto a cambiar. Razor sustituye {0} por el DisplayName del campo.
public static class ValidationMessages
{
    public const string Required = "El campo {0} es obligatorio.";
    public const string Rfc = "El RFC debe tener entre 12 y 13 caracteres.";
    public const string Phone = "El teléfono debe tener exactamente 10 dígitos.";
    public const string Email = "El email no tiene un formato válido.";
    public const string PostalCode = "El código postal debe tener 5 dígitos.";
    public const string StateCode = "El estado debe ser un código de 2 caracteres.";
    public const string SumInsuredPositive = "La suma asegurada debe ser mayor a cero.";
}
