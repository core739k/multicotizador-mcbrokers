using McBrokers.Domain.Catalog;

namespace McBrokers.Domain.Tests.Catalog;

public class VehicleMasterTests
{
    [Fact]
    public void Create_with_valid_inputs_succeeds()
    {
        var result = VehicleMaster.Create(
            year: 2025,
            brand: "CHEVROLET",
            model: "AVEO",
            version: "LT",
            bodyType: "SEDAN",
            transmission: VehicleTransmission.Manual,
            doors: 4,
            cylinders: 4);

        result.IsSuccess.Should().BeTrue();
        var vm = result.Value;
        vm.Id.Should().NotBe(Guid.Empty);
        vm.Year.Should().Be(2025);
        vm.Brand.Should().Be("CHEVROLET");
        vm.Model.Should().Be("AVEO");
        vm.Version.Should().Be("LT");
        vm.BodyType.Should().Be("SEDAN");
        vm.Transmission.Should().Be(VehicleTransmission.Manual);
        vm.Doors.Should().Be(4);
        vm.Cylinders.Should().Be(4);
        vm.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData(1899)]
    [InlineData(2200)]
    [InlineData(0)]
    public void Create_rejects_out_of_range_year(int year)
    {
        var result = VehicleMaster.Create(year, "X", "Y", "Z", "SEDAN", VehicleTransmission.Manual, 4, 4);
        result.IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_brand(string? brand)
    {
        var result = VehicleMaster.Create(2025, brand!, "AVEO", "LT", "SEDAN", VehicleTransmission.Manual, 4, 4);
        result.IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Create_rejects_empty_model(string? model)
    {
        var result = VehicleMaster.Create(2025, "CHEVROLET", model!, "LT", "SEDAN", VehicleTransmission.Manual, 4, 4);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Create_trims_string_fields()
    {
        var vm = VehicleMaster.Create(2025, "  CHEVROLET  ", " AVEO ", " LT ", " SEDAN ", VehicleTransmission.Manual, 4, 4).Value;

        vm.Brand.Should().Be("CHEVROLET");
        vm.Model.Should().Be("AVEO");
        vm.Version.Should().Be("LT");
        vm.BodyType.Should().Be("SEDAN");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Create_rejects_negative_doors(int doors)
    {
        var result = VehicleMaster.Create(2025, "X", "Y", "Z", "SEDAN", VehicleTransmission.Manual, doors, 4);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_marks_master_inactive()
    {
        var vm = VehicleMaster.Create(2025, "X", "Y", "Z", "SEDAN", VehicleTransmission.Manual, 4, 4).Value;
        vm.Deactivate();
        vm.IsActive.Should().BeFalse();
    }
}
