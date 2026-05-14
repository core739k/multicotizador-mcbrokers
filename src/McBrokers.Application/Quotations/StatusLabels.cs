namespace McBrokers.Application.Quotations;

using McBrokers.Domain.Quotations;

// Helper de presentación. Mantiene los enums Domain en su naming técnico
// (Succeeded, Failed, Timeout, ...) y traduce a es-MX para la UI.
// No reemplaza al enum — vive en Application porque también lo consumirá
// la app móvil cuando llegue Fase 7.
public static class StatusLabels
{
    public static string Spanish(QuotationStatus status) => status switch
    {
        QuotationStatus.Pending => "En proceso",
        QuotationStatus.Partial => "Parcial",
        QuotationStatus.Completed => "Completada",
        QuotationStatus.Failed => "No disponible",
        _ => status.ToString(),
    };

    public static string Spanish(QuotationInsurerStatus status) => status switch
    {
        QuotationInsurerStatus.Pending => "En proceso",
        QuotationInsurerStatus.Succeeded => "Cotización obtenida",
        QuotationInsurerStatus.Failed => "No disponible",
        QuotationInsurerStatus.Timeout => "Sin respuesta",
        QuotationInsurerStatus.InsurerDown => "Sin respuesta",
        QuotationInsurerStatus.NotCovered => "No disponible",
        _ => status.ToString(),
    };
}
