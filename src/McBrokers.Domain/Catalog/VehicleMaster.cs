using McBrokers.SharedKernel;

namespace McBrokers.Domain.Catalog;

public sealed class VehicleMaster
{
    private const int MinYear = 1900;
    private const int MaxYear = 2100;

    public Guid Id { get; }
    public int Year { get; }
    public string Brand { get; private set; }
    public string Model { get; private set; }
    public string Version { get; private set; }
    public string BodyType { get; private set; }
    public VehicleTransmission Transmission { get; private set; }
    public int Doors { get; private set; }
    public int Cylinders { get; private set; }
    public bool IsActive { get; private set; }

    private VehicleMaster(
        Guid id, int year, string brand, string model, string version,
        string bodyType, VehicleTransmission transmission, int doors, int cylinders, bool isActive)
    {
        Id = id;
        Year = year;
        Brand = brand;
        Model = model;
        Version = version;
        BodyType = bodyType;
        Transmission = transmission;
        Doors = doors;
        Cylinders = cylinders;
        IsActive = isActive;
    }

    public static Result<VehicleMaster> Create(
        int year, string brand, string model, string version,
        string bodyType, VehicleTransmission transmission, int doors, int cylinders)
    {
        if (year < MinYear || year > MaxYear)
        {
            return Result<VehicleMaster>.Failure($"Year must be between {MinYear} and {MaxYear}.");
        }

        if (string.IsNullOrWhiteSpace(brand)) return Result<VehicleMaster>.Failure("Brand must not be empty.");
        if (string.IsNullOrWhiteSpace(model)) return Result<VehicleMaster>.Failure("Model must not be empty.");
        if (string.IsNullOrWhiteSpace(version)) return Result<VehicleMaster>.Failure("Version must not be empty.");
        if (doors < 0) return Result<VehicleMaster>.Failure("Doors must be zero or positive.");
        if (cylinders < 0) return Result<VehicleMaster>.Failure("Cylinders must be zero or positive.");

        return Result<VehicleMaster>.Success(new VehicleMaster(
            Guid.NewGuid(),
            year,
            brand.Trim(),
            model.Trim(),
            version.Trim(),
            (bodyType ?? string.Empty).Trim(),
            transmission,
            doors,
            cylinders,
            isActive: true));
    }

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;
}
