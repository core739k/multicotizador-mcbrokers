using McBrokers.Domain.Insurers.AxaDxn;

namespace McBrokers.Domain.Tests.Insurers.AxaDxn;

public class AxaDxnBusinessTests
{
    private static readonly Guid AConfigId = Guid.NewGuid();

    [Fact]
    public void Create_with_valid_inputs_succeeds()
    {
        var result = AxaDxnBusiness.Create(
            AConfigId, AxaDxnBusinessName.Strm,
            polizaAutos: "UCC360860000",
            polizaPickup: "UCC360890000",
            mes: 5);

        result.IsSuccess.Should().BeTrue();
        var b = result.Value;
        b.AxaDxnConfigId.Should().Be(AConfigId);
        b.Nombre.Should().Be(AxaDxnBusinessName.Strm);
        b.PolizaAutos.Should().Be("UCC360860000");
        b.PolizaPickup.Should().Be("UCC360890000");
        b.Mes.Should().Be(5);
    }

    [Fact]
    public void Create_allows_null_policies_for_businesses_not_yet_configured()
    {
        var result = AxaDxnBusiness.Create(AConfigId, AxaDxnBusinessName.Bimbo, null, null, 1);

        result.IsSuccess.Should().BeTrue();
        result.Value.PolizaAutos.Should().BeNull();
        result.Value.PolizaPickup.Should().BeNull();
    }

    [Fact]
    public void Create_treats_whitespace_only_policies_as_null()
    {
        var result = AxaDxnBusiness.Create(AConfigId, AxaDxnBusinessName.Mcb, "   ", "", 1);

        result.IsSuccess.Should().BeTrue();
        result.Value.PolizaAutos.Should().BeNull();
        result.Value.PolizaPickup.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void Create_rejects_out_of_range_mes(int mes)
    {
        var result = AxaDxnBusiness.Create(AConfigId, AxaDxnBusinessName.Strm, null, null, mes);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Update_can_set_policies()
    {
        var b = AxaDxnBusiness.Create(AConfigId, AxaDxnBusinessName.Strm, null, null, 1).Value;

        var result = b.Update("UCC1", "UCC2", 7);

        result.IsSuccess.Should().BeTrue();
        b.PolizaAutos.Should().Be("UCC1");
        b.PolizaPickup.Should().Be("UCC2");
        b.Mes.Should().Be(7);
    }

    [Fact]
    public void Update_can_clear_policies()
    {
        var b = AxaDxnBusiness.Create(AConfigId, AxaDxnBusinessName.Strm, "UCC1", "UCC2", 5).Value;

        var result = b.Update(null, null, 5);

        result.IsSuccess.Should().BeTrue();
        b.PolizaAutos.Should().BeNull();
        b.PolizaPickup.Should().BeNull();
    }
}
