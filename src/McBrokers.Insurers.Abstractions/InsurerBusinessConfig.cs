namespace McBrokers.Insurers.Abstractions;

/// <summary>
/// Discriminated union (Option A) que carga configuración por aseguradora hacia el adapter.
/// Cada adapter conoce su subclase concreta y hace pattern matching. El campo BusinessConfig
/// en InsurerQuoteRequest / InsurerEmitRequest es nullable porque la migración del modelo
/// genérico (InsurerCredentials) a typed-config se hace por aseguradora — empezando por
/// AXA DXN. Las otras 3 siguen recibiendo Credentials hasta que se expanda el patrón.
/// </summary>
public abstract record InsurerBusinessConfig;

/// <summary>
/// Configuración de AXA DXN al momento de cotizar/emitir. Es la proyección al runtime
/// del agregado Domain.Insurers.AxaDxn.AxaDxnConfig + el negocio seleccionado (por el
/// vendor o por default), con la póliza y mes resueltos.
/// </summary>
public sealed record AxaDxnAdapterConfig(
    string Usuario,
    string Password,
    string Tarifa,
    string TarifaPickup,
    int Descuento,
    int DescuentoPickup,
    int MesPolizaDefault,
    string SelectedBusinessName,
    string? PolizaAutos,
    string? PolizaPickup,
    int BusinessMes) : InsurerBusinessConfig;
