namespace McBrokers.Domain.Quotations;

public enum PackageCode
{
    Amplia = 1,
    Limitada = 2,
    ResponsabilidadCivil = 3,
}

public enum PaymentMode
{
    Annual = 1,
    Semestral = 2,
    Trimestral = 3,
    Monthly = 4,
}

public enum ValuationType
{
    Commercial = 1,
    CommercialPlus10 = 2,
    Agreed = 3,
    AgreedPlus10 = 4,
    Invoice = 5,
}

public enum QuotationStatus
{
    Pending = 0,
    Partial = 1,
    Completed = 2,
    Failed = 3,
}

public enum QuotationInsurerStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Timeout = 3,
    InsurerDown = 4,
    NotCovered = 5,
}

public enum ErrorCategory
{
    None = 0,
    Technical = 1,
    Business = 2,
    InsurerDown = 3,
}

public enum Gender
{
    Male = 1,
    Female = 2,
}
