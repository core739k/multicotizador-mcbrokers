using McBrokers.Domain.Insurers;

namespace McBrokers.Domain.Tests.Insurers;

public class InsurerTests
{
    [Fact]
    public void Create_with_valid_inputs_succeeds()
    {
        var result = Insurer.Create(InsurerCode.Gnp, "Grupo Nacional Provincial", displayOrder: 1);

        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be(InsurerCode.Gnp);
        result.Value.Name.Should().Be("Grupo Nacional Provincial");
        result.Value.IsEnabled.Should().BeTrue("new insurers default to enabled");
        result.Value.DisplayOrder.Should().Be(1);
        result.Value.Id.Should().NotBe(Guid.Empty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_name(string? name)
    {
        var result = Insurer.Create(InsurerCode.Gnp, name!, displayOrder: 1);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("name");
    }

    [Fact]
    public void Create_trims_name()
    {
        var insurer = Insurer.Create(InsurerCode.Gnp, "  GNP  ", displayOrder: 1).Value;

        insurer.Name.Should().Be("GNP");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Create_rejects_negative_display_order(int order)
    {
        var result = Insurer.Create(InsurerCode.Gnp, "GNP", displayOrder: order);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Disable_marks_insurer_disabled()
    {
        var insurer = Insurer.Create(InsurerCode.Gnp, "GNP", displayOrder: 1).Value;

        insurer.Disable();

        insurer.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Enable_marks_insurer_enabled()
    {
        var insurer = Insurer.Create(InsurerCode.Gnp, "GNP", displayOrder: 1).Value;
        insurer.Disable();

        insurer.Enable();

        insurer.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Rename_updates_name_when_valid()
    {
        var insurer = Insurer.Create(InsurerCode.Gnp, "Old", displayOrder: 1).Value;

        var result = insurer.Rename("New");

        result.IsSuccess.Should().BeTrue();
        insurer.Name.Should().Be("New");
    }

    [Fact]
    public void Rename_rejects_empty_value()
    {
        var insurer = Insurer.Create(InsurerCode.Gnp, "Old", displayOrder: 1).Value;

        var result = insurer.Rename("  ");

        result.IsSuccess.Should().BeFalse();
        insurer.Name.Should().Be("Old", because: "the previous name must survive a failed rename");
    }

    [Fact]
    public void SetDisplayOrder_updates_order_when_non_negative()
    {
        var insurer = Insurer.Create(InsurerCode.Gnp, "GNP", displayOrder: 1).Value;

        var result = insurer.SetDisplayOrder(5);

        result.IsSuccess.Should().BeTrue();
        insurer.DisplayOrder.Should().Be(5);
    }

    [Fact]
    public void SetDisplayOrder_rejects_negative_value()
    {
        var insurer = Insurer.Create(InsurerCode.Gnp, "GNP", displayOrder: 1).Value;

        var result = insurer.SetDisplayOrder(-3);

        result.IsSuccess.Should().BeFalse();
        insurer.DisplayOrder.Should().Be(1);
    }

    [Fact]
    public void SetLogoUrl_accepts_https_url()
    {
        var insurer = Insurer.Create(InsurerCode.Gnp, "GNP", displayOrder: 1).Value;

        var result = insurer.SetLogoUrl("https://cdn.mcbrokers.com.mx/gnp.png");

        result.IsSuccess.Should().BeTrue();
        insurer.LogoUrl.Should().Be("https://cdn.mcbrokers.com.mx/gnp.png");
    }

    [Fact]
    public void SetLogoUrl_accepts_null_to_clear()
    {
        var insurer = Insurer.Create(InsurerCode.Gnp, "GNP", displayOrder: 1).Value;
        insurer.SetLogoUrl("https://cdn.mcbrokers.com.mx/gnp.png");

        insurer.SetLogoUrl(null).IsSuccess.Should().BeTrue();

        insurer.LogoUrl.Should().BeNull();
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com/logo.png")]
    [InlineData("http://insecure.com/logo.png")]
    public void SetLogoUrl_rejects_invalid_or_insecure_url(string url)
    {
        var insurer = Insurer.Create(InsurerCode.Gnp, "GNP", displayOrder: 1).Value;

        var result = insurer.SetLogoUrl(url);

        result.IsSuccess.Should().BeFalse();
    }
}
