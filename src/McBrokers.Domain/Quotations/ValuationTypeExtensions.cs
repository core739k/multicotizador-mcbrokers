namespace McBrokers.Domain.Quotations;

public static class ValuationTypeExtensions
{
    // ¿Debe el adapter enviar SumInsured a la aseguradora?
    // - Agreed / AgreedPlus10 / Invoice → SÍ: el valor lo determina el cliente
    //   (valor convenido o factura), la aseguradora debe usar ese.
    // - Commercial / CommercialPlus10 → NO: la aseguradora calcula el valor
    //   internamente desde su tarifa/libro azul; se manda placeholder "0".
    public static bool ShouldSendSumInsured(this ValuationType valuation) => valuation switch
    {
        ValuationType.Agreed or ValuationType.AgreedPlus10 or ValuationType.Invoice => true,
        ValuationType.Commercial or ValuationType.CommercialPlus10 => false,
        _ => false,
    };
}
