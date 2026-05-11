namespace McBrokers.Application.Catalog;

public sealed record CatalogImportRow(
    int Year,
    string Brand,
    string Model,
    string Version,
    string ExternalClave,
    string? BodyType,
    string? Transmission);
