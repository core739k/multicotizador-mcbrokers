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
    decimal Fees);

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
