namespace McBrokers.Application.Quotations;

// Defaults globales de coberturas y deducibles para el wizard de cotización.
// Fase A: bindeados desde appsettings.json ("Cotizacion:DefaultCoverages") —
// editables sin recompilar pero todavía un único set para todas las
// aseguradoras. Fase B (sprint propio) implementará InsurerCoverageDefault
// por aseguradora con admin UI; ver REQUIREMENTS.md §4.2.
public sealed class DefaultCoverages
{
    public const string ConfigSection = "Cotizacion:DefaultCoverages";

    // % deducible sobre el valor del vehículo cuando ocurre un siniestro de DM.
    public decimal MaterialDamagesDeductiblePct { get; set; } = 5m;

    // % deducible sobre el valor del vehículo cuando se reporta robo total.
    public decimal RobberyDeductiblePct { get; set; } = 10m;

    // Suma asegurada para Gastos Médicos a Ocupantes (MXN).
    public decimal MedicalExpensesSumInsured { get; set; } = 200_000m;

    // Suma asegurada para Responsabilidad Civil (MXN).
    public decimal CivilLiabilitySumInsured { get; set; } = 3_000_000m;

    // Sets disponibles en los selectores de la card de resultados.
    // Defaults VACÍOS: el binder de Microsoft.Extensions.Configuration
    // concatena arrays JSON con arrays existentes en el POCO en lugar de
    // reemplazarlos. Con defaults [5,10,15,20] + JSON [5,10,15,20] el
    // resultado era 8 elementos duplicados — los dropdowns mostraban
    // las opciones dos veces. appsettings.json es la única fuente de
    // verdad. En Fase 2 estos arrays migran a BD (cat_deducibles,
    // cat_valor_estimado, rel_deducible_valor_estimado).
    public decimal[] AvailableDMPct { get; set; } = Array.Empty<decimal>();
    public decimal[] AvailableRTPct { get; set; } = Array.Empty<decimal>();
    public decimal[] AvailableGMO { get; set; } = Array.Empty<decimal>();
}
