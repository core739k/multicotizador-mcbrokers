using McBrokers.SharedKernel;

namespace McBrokers.Domain.Insurers;

public sealed class Insurer
{
    public Guid Id { get; }
    public InsurerCode Code { get; }
    public string Name { get; private set; }
    public bool IsEnabled { get; private set; }
    public int DisplayOrder { get; private set; }
    public string? LogoUrl { get; private set; }

    private Insurer(Guid id, InsurerCode code, string name, bool isEnabled, int displayOrder, string? logoUrl)
    {
        Id = id;
        Code = code;
        Name = name;
        IsEnabled = isEnabled;
        DisplayOrder = displayOrder;
        LogoUrl = logoUrl;
    }

    public static Result<Insurer> Create(InsurerCode code, string name, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<Insurer>.Failure("Insurer name must not be empty.");
        }

        if (displayOrder < 0)
        {
            return Result<Insurer>.Failure("Insurer displayOrder must be zero or positive.");
        }

        return Result<Insurer>.Success(new Insurer(
            Guid.NewGuid(),
            code,
            name.Trim(),
            isEnabled: true,
            displayOrder,
            logoUrl: null));
    }

    public void Enable() => IsEnabled = true;

    public void Disable() => IsEnabled = false;

    public Result<Insurer> Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return Result<Insurer>.Failure("Insurer name must not be empty.");
        }

        Name = newName.Trim();
        return Result<Insurer>.Success(this);
    }

    public Result<Insurer> SetDisplayOrder(int order)
    {
        if (order < 0)
        {
            return Result<Insurer>.Failure("Display order must be zero or positive.");
        }

        DisplayOrder = order;
        return Result<Insurer>.Success(this);
    }

    public Result<Insurer> SetLogoUrl(string? url)
    {
        if (url is null)
        {
            LogoUrl = null;
            return Result<Insurer>.Success(this);
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || parsed.Scheme != Uri.UriSchemeHttps)
        {
            return Result<Insurer>.Failure("Logo URL must be an absolute https URL.");
        }

        LogoUrl = parsed.ToString();
        return Result<Insurer>.Success(this);
    }
}
