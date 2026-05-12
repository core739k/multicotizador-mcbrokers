namespace McBrokers.Domain.Insurers.AxaDxn;

/// <summary>
/// Negocios bajo los que MCBrokers opera con AXA DXN. Cada negocio tiene su propia
/// póliza de Autos y, separadamente, su póliza de Pickup, más un mes de renovación.
/// Solo STRM tiene valores reales hoy; los demás existen para captura futura.
/// </summary>
public enum AxaDxnBusinessName
{
    Bimbo = 0,
    Strm = 1,
    Mcb = 2,
    Caja = 3,
    Ctbr = 4,
}
