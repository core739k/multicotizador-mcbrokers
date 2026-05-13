using McBrokers.Domain.Insurers;

namespace McBrokers.Domain.Tests.Insurers;

public class InsurerLogoMappingTests
{
    [Theory]
    [InlineData(InsurerCode.Gnp, "gnp")]
    [InlineData(InsurerCode.Qua, "qualitas")]
    [InlineData(InsurerCode.Ana, "ana")]
    [InlineData(InsurerCode.AxaDxn, "axa_dxn")]
    [InlineData(InsurerCode.AxaCol, "axa_col")]
    public void FileNameFor_maps_each_code_to_legacy_filename(InsurerCode code, string expected)
    {
        InsurerLogoMapping.FileNameFor(code).Should().Be(expected);
    }

    [Fact]
    public void DefaultRelativeUrl_combines_filename_with_png_extension()
    {
        InsurerLogoMapping.DefaultRelativeUrl(InsurerCode.AxaDxn)
            .Should().Be("/img/logos/axa_dxn.png");
    }
}
