namespace McBrokers.Insurers.AxaDxn.Mapping.Catalog;

public sealed record AxaDxnCatalogRawRow(
    string? IdMarca,
    string? IdTipoVehiculo,
    string? Descripcion,
    string? IdTipo,
    string? ClaveAmis,
    int? ModeloDesde,
    int? ModeloHasta);
