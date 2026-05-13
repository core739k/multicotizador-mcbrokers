namespace McBrokers.Domain.Insurers;

// Mapeo entre InsurerCode (enum corto: Qua/Gnp/...) y los nombres de archivo
// históricamente usados en el sistema (qualitas/gnp/ana/axa_dxn/axa_col).
// Estos son los nombres heredados del legado Angular y los archivos
// distribuidos en wwwroot/img/logos/. Si marketing reemplaza los logos,
// sobreescribe los archivos con el mismo nombre — el mapeo no cambia.
public static class InsurerLogoMapping
{
    public const string LogosRelativeRoot = "/img/logos/";
    public const string DefaultExtension = ".png";

    public static string FileNameFor(InsurerCode code) => code switch
    {
        InsurerCode.Gnp => "gnp",
        InsurerCode.Qua => "qualitas",
        InsurerCode.Ana => "ana",
        InsurerCode.AxaDxn => "axa_dxn",
        InsurerCode.AxaCol => "axa_col",
        _ => code.ToString().ToLowerInvariant(),
    };

    public static string DefaultRelativeUrl(InsurerCode code) =>
        $"{LogosRelativeRoot}{FileNameFor(code)}{DefaultExtension}";
}
