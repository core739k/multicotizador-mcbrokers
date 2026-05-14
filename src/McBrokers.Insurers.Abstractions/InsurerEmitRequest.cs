using McBrokers.Domain.Quotations;

namespace McBrokers.Insurers.Abstractions;

public sealed record InsurerEmitRequest(
    string CorrelationId,
    InsurerCredentials Credentials,
    InsurerConnectionConfig Connection,
    string ExternalQuoteRef,
    EmissionVehicleData Vehicle,
    EmissionContactData Contractor,
    EmissionContactData HabitualDriver,
    decimal PremiumTotal,
    decimal PremiumNet,
    decimal Tax,
    decimal Fees,
    ValuationType Valuation,
    // XML de respuesta de cotización (la SOAP previa de AXA, ya limpiada del wrapper soapenv).
    // AXA DXN/COPSIS lo necesita embebido en SOLICITUDEMISION/CotizaAutoRespuesta. Otros
    // adapters lo ignoran. Null cuando el caller no pudo leer el blob de cotización.
    string? RawQuoteResponseXml = null,
    // Clave externa del agente para el campo <vendedor> en COPSIS. Otros adapters lo ignoran.
    string? AgentExternalCode = null,
    InsurerBusinessConfig? BusinessConfig = null);

public sealed record EmissionVehicleData(
    int Year,
    string Brand,
    string Model,
    string Version,
    string ExternalClave,
    string Plate,
    string EngineNumber,
    string SerialNumber);

public sealed record EmissionContactData(
    string FirstName,
    string LastNamePaternal,
    string LastNameMaternal,
    string Rfc,
    string Street,
    string ExteriorNumber,
    string? InteriorNumber,
    string Neighborhood,
    string City,
    string StateCode,
    string PostalCode,
    string Phone,
    string Email);
