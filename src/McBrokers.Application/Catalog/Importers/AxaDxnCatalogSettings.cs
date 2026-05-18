namespace McBrokers.Application.Catalog.Importers;

/// <summary>
/// Override opcional del endpoint del WS de catálogo AXA DXN. Si es null,
/// el orquestador usa el default hardcodeado (SolicitudPolizasService en
/// el host de producción). NO se lee de InsurerConfig.EndpointUrl porque
/// ese campo apunta al WS de cotización (otro path en el mismo host).
/// Se hidrata desde appsettings: "AxaDxn:CatalogEndpoint".
/// </summary>
public sealed record AxaDxnCatalogSettings(string? CatalogEndpointOverride);
