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
}
