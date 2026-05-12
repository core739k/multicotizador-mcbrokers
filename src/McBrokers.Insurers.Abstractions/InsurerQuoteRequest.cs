using McBrokers.Domain.Quotations;

namespace McBrokers.Insurers.Abstractions;

public sealed record InsurerQuoteRequest(
    string CorrelationId,
    InsurerCredentials Credentials,
    InsurerConnectionConfig Connection,
    VehicleSelection Vehicle,
    PackageCode Package,
    string PackageExternalCode,
    PaymentMode PaymentMode,
    ValuationType ValuationType,
    decimal SumInsured,
    DeductiblesAndSums Deductibles,
    ContactInfo Contractor,
    DriverInfo HabitualDriver,
    string PostalCode);

public sealed record InsurerCredentials(string Username, string Password, string? BusinessUnit);

public sealed record InsurerConnectionConfig(string EndpointUrl, int TimeoutSeconds, int MaxRetries);

public sealed record VehicleSelection(
    int Year,
    string Brand,
    string Model,
    string Version,
    string ExternalClave);

public sealed record DeductiblesAndSums(
    decimal MaterialDamagesDeductiblePct,
    decimal RobberyDeductiblePct,
    decimal MedicalExpensesSumInsured,
    decimal CivilLiabilitySumInsured);

public sealed record ContactInfo(
    string FirstName,
    string LastNamePaternal,
    string LastNameMaternal,
    string PostalCode,
    Gender Gender,
    DateOnly DateOfBirth);

public sealed record DriverInfo(
    string PostalCode,
    Gender Gender,
    DateOnly DateOfBirth);
