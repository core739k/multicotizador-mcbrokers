using McBrokers.Domain.Insurers;
using McBrokers.Domain.Quotations;

namespace McBrokers.Domain.Tests.Quotations;

public class PackageCoverageMatrixTests
{
    [Theory]
    [InlineData(InsurerCode.Gnp, PackageCode.Amplia)]
    [InlineData(InsurerCode.Gnp, PackageCode.Limitada)]
    [InlineData(InsurerCode.Gnp, PackageCode.ResponsabilidadCivil)]
    [InlineData(InsurerCode.Qua, PackageCode.Amplia)]
    [InlineData(InsurerCode.Qua, PackageCode.Limitada)]
    [InlineData(InsurerCode.Qua, PackageCode.ResponsabilidadCivil)]
    [InlineData(InsurerCode.Ana, PackageCode.Amplia)]
    [InlineData(InsurerCode.Ana, PackageCode.Limitada)]
    [InlineData(InsurerCode.Ana, PackageCode.ResponsabilidadCivil)]
    [InlineData(InsurerCode.AxaDxn, PackageCode.Amplia)]
    [InlineData(InsurerCode.AxaDxn, PackageCode.Limitada)]
    [InlineData(InsurerCode.AxaDxn, PackageCode.ResponsabilidadCivil)]
    [InlineData(InsurerCode.AxaCol, PackageCode.Amplia)]
    [InlineData(InsurerCode.AxaCol, PackageCode.Limitada)]
    [InlineData(InsurerCode.AxaCol, PackageCode.ResponsabilidadCivil)]
    public void Compute_returns_three_badges_for_every_insurer_and_package(InsurerCode code, PackageCode package)
    {
        var badges = PackageCoverageMatrix.Compute(code, package);

        badges.Should().HaveCount(3, because: "Protección Legal + RC Ocupantes + Asistencia Vial");
    }

    [Theory]
    [InlineData(InsurerCode.Gnp)]
    [InlineData(InsurerCode.Qua)]
    [InlineData(InsurerCode.Ana)]
    [InlineData(InsurerCode.AxaDxn)]
    [InlineData(InsurerCode.AxaCol)]
    public void First_badge_is_Proteccion_Legal_and_amparada(InsurerCode code)
    {
        var badges = PackageCoverageMatrix.Compute(code, PackageCode.Amplia);

        badges[0].Label.Should().Be("Protección Legal");
        badges[0].Amparado.Should().BeTrue();
    }

    [Theory]
    [InlineData(InsurerCode.Gnp)]
    [InlineData(InsurerCode.Qua)]
    [InlineData(InsurerCode.Ana)]
    [InlineData(InsurerCode.AxaDxn)]
    [InlineData(InsurerCode.AxaCol)]
    public void Second_badge_is_RC_Ocupantes_and_amparada(InsurerCode code)
    {
        var badges = PackageCoverageMatrix.Compute(code, PackageCode.Amplia);

        badges[1].Label.Should().Be("RC Ocupantes");
        badges[1].Amparado.Should().BeTrue();
    }

    [Theory]
    [InlineData(InsurerCode.AxaDxn, "Club AXA")]
    [InlineData(InsurerCode.AxaCol, "Club AXA")]
    [InlineData(InsurerCode.Qua, "Asistencia Vial Plus")]
    [InlineData(InsurerCode.Gnp, "Asist. Vial")]
    [InlineData(InsurerCode.Ana, "Asistencia")]
    public void Third_badge_is_per_insurer_asistencia_vial(InsurerCode code, string expectedLabel)
    {
        var badges = PackageCoverageMatrix.Compute(code, PackageCode.Amplia);

        badges[2].Label.Should().Be(expectedLabel);
        badges[2].Amparado.Should().BeTrue();
    }

    [Theory]
    [InlineData(PackageCode.Amplia)]
    [InlineData(PackageCode.Limitada)]
    [InlineData(PackageCode.ResponsabilidadCivil)]
    public void Asistencia_vial_label_is_same_across_packages(PackageCode package)
    {
        // El nombre comercial de la asistencia no cambia por paquete; solo por aseguradora.
        var gnpAmplia = PackageCoverageMatrix.Compute(InsurerCode.Gnp, package);
        gnpAmplia[2].Label.Should().Be("Asist. Vial");
    }
}
