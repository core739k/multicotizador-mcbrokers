using McBrokers.Domain.Insurers;

namespace McBrokers.Domain.Quotations;

public sealed record CoverageBadge(string Label, bool Amparado);

// Coberturas "informativas" para la tarjeta de resultados — sirven para que el
// vendedor confirme qué amparos ofrece el paquete contratado sin tener que
// abrir el detalle. Las tres son siempre amparadas en los tres paquetes
// soportados (Amplia/Limitada/RC) según la documentación oficial de cada
// aseguradora (ver /Documentación/Documentación Servicio *.md §5).
//
// Si en el futuro una aseguradora deja de incluir alguna en algún paquete,
// se ajusta acá. Cuando esto se vuelva administrable (Fase B junto con
// deducibles), migrará a la tabla InsurerCoverageDefault.
public static class PackageCoverageMatrix
{
    public const string ProteccionLegalLabel = "Protección Legal";
    public const string RcOcupantesLabel = "RC Ocupantes";

    public static IReadOnlyList<CoverageBadge> Compute(InsurerCode code, PackageCode package) =>
        new[]
        {
            new CoverageBadge(ProteccionLegalLabel, IncludesProteccionLegal(code, package)),
            new CoverageBadge(RcOcupantesLabel, IncludesRcOcupantes(code, package)),
            new CoverageBadge(AsistenciaVialLabel(code), IncludesAsistenciaVial(code, package)),
        };

    // Nombre comercial de la cobertura de Asistencia Vial varía por aseguradora.
    public static string AsistenciaVialLabel(InsurerCode code) => code switch
    {
        InsurerCode.AxaDxn or InsurerCode.AxaCol => "Club AXA",
        InsurerCode.Qua => "Asistencia Vial Plus",
        InsurerCode.Gnp => "Asist. Vial",
        InsurerCode.Ana => "Asistencia",
        _ => "Asistencia",
    };

    // Las tres son amparadas en los tres paquetes para las 5 aseguradoras
    // hoy (verificado contra documentación oficial). Mantengo métodos por
    // separado para que ajustes futuros (ej. paquete RC sin Asistencia en
    // alguna aseguradora) sean targeted.
    private static bool IncludesProteccionLegal(InsurerCode code, PackageCode package) => true;
    private static bool IncludesRcOcupantes(InsurerCode code, PackageCode package) => true;
    private static bool IncludesAsistenciaVial(InsurerCode code, PackageCode package) => true;
}
