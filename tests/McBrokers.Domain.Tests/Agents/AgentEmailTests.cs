using McBrokers.Domain.Agents;

namespace McBrokers.Domain.Tests.Agents;

public class AgentEmailTests
{
    [Theory]
    [InlineData("user@mcbrokers.com.mx")]
    [InlineData("esteban.contreras@mcbrokers.com.mx")]
    [InlineData("a@mcbrokers.com.mx")]
    public void Create_with_valid_mcbrokers_email_succeeds(string input)
    {
        var result = AgentEmail.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(input.Trim().ToLowerInvariant());
    }

    [Theory]
    [InlineData("USER@MCBROKERS.COM.MX", "user@mcbrokers.com.mx")]
    [InlineData("Esteban.Contreras@McBrokers.COM.MX", "esteban.contreras@mcbrokers.com.mx")]
    [InlineData("  user@mcbrokers.com.mx  ", "user@mcbrokers.com.mx")]
    public void Create_normalizes_to_lowercase_and_trims(string input, string expected)
    {
        var result = AgentEmail.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("user@gmail.com")]
    [InlineData("user@mcbrokers.com")]            // falta .mx
    [InlineData("user@mcbrokers.mx")]             // falta .com
    [InlineData("user@mc.brokers.com.mx")]        // dominio distinto
    [InlineData("user@sub.mcbrokers.com.mx")]     // subdominio no permitido
    [InlineData("user@mcbrokers.com.mx.attacker.io")] // intento de bypass
    public void Create_with_non_mcbrokers_domain_fails(string input)
    {
        var result = AgentEmail.Create(input);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("@mcbrokers.com.mx")]             // sin local part
    [InlineData("user@")]                         // sin dominio
    [InlineData("user@@mcbrokers.com.mx")]        // doble @
    public void Create_with_invalid_format_fails(string? input)
    {
        var result = AgentEmail.Create(input!);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Equality_is_case_insensitive()
    {
        var a = AgentEmail.Create("user@mcbrokers.com.mx").Value;
        var b = AgentEmail.Create("USER@MCBROKERS.COM.MX").Value;

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void ToString_returns_canonical_value()
    {
        var email = AgentEmail.Create("Esteban.Contreras@McBrokers.COM.MX").Value;

        email.ToString().Should().Be("esteban.contreras@mcbrokers.com.mx");
    }

    [Fact]
    public void Implicit_string_conversion_returns_canonical_value()
    {
        var email = AgentEmail.Create("User@McBrokers.com.mx").Value;

        string asString = email;

        asString.Should().Be("user@mcbrokers.com.mx");
    }
}
