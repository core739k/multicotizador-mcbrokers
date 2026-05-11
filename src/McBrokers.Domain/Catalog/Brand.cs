using McBrokers.SharedKernel;

namespace McBrokers.Domain.Catalog;

public sealed class Brand
{
    public Guid Id { get; }
    public string CanonicalName { get; private set; }

    private Brand(Guid id, string canonicalName)
    {
        Id = id;
        CanonicalName = canonicalName;
    }

    public static Result<Brand> Create(string canonicalName)
    {
        if (string.IsNullOrWhiteSpace(canonicalName))
        {
            return Result<Brand>.Failure("Brand canonical name must not be empty.");
        }

        return Result<Brand>.Success(new Brand(Guid.NewGuid(), canonicalName.Trim().ToUpperInvariant()));
    }
}

public sealed class BrandSynonym
{
    public Guid Id { get; }
    public Guid BrandId { get; }
    public string SynonymText { get; }
    public string Source { get; }

    private BrandSynonym(Guid id, Guid brandId, string synonymText, string source)
    {
        Id = id;
        BrandId = brandId;
        SynonymText = synonymText;
        Source = source;
    }

    public static Result<BrandSynonym> Create(Guid brandId, string synonymText, string source)
    {
        if (string.IsNullOrWhiteSpace(synonymText))
        {
            return Result<BrandSynonym>.Failure("Synonym text must not be empty.");
        }

        return Result<BrandSynonym>.Success(new BrandSynonym(
            Guid.NewGuid(), brandId, synonymText.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(source) ? "manual" : source.Trim()));
    }
}
