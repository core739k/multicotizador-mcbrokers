using McBrokers.Domain.Insurers;

namespace McBrokers.Domain.Tests.Insurers;

public class InsurerConfigTests
{
    private static readonly Guid AnInsurerId = Guid.NewGuid();

    [Fact]
    public void Create_with_valid_inputs_succeeds()
    {
        var result = InsurerConfig.Create(
            insurerId: AnInsurerId,
            endpointUrl: "https://insurer.example.com/ws",
            businessNumber: "12345",
            agentCode: "AGT001",
            keyVaultSecretName: "insurers--gnp--credentials",
            timeoutSeconds: 30,
            maxRetries: 3);

        result.IsSuccess.Should().BeTrue();
        var cfg = result.Value;
        cfg.InsurerId.Should().Be(AnInsurerId);
        cfg.EndpointUrl.Should().Be("https://insurer.example.com/ws");
        cfg.BusinessNumber.Should().Be("12345");
        cfg.AgentCode.Should().Be("AGT001");
        cfg.KeyVaultSecretName.Should().Be("insurers--gnp--credentials");
        cfg.TimeoutSeconds.Should().Be(30);
        cfg.MaxRetries.Should().Be(3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com")]
    [InlineData("http://insecure.example.com")]
    public void Create_rejects_invalid_endpoint(string? endpoint)
    {
        var result = InsurerConfig.Create(
            AnInsurerId, endpoint!, "1", "A", "kv", 30, 3);

        result.IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(601)] // > 10 min — sanity ceiling
    public void Create_rejects_invalid_timeout(int timeout)
    {
        var result = InsurerConfig.Create(
            AnInsurerId, "https://x.com", "1", "A", "kv", timeout, 3);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Create_rejects_negative_retries()
    {
        var result = InsurerConfig.Create(
            AnInsurerId, "https://x.com", "1", "A", "kv", 30, maxRetries: -1);

        result.IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_key_vault_secret_name(string? kvName)
    {
        var result = InsurerConfig.Create(
            AnInsurerId, "https://x.com", "1", "A", kvName!, 30, 3);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("vault");
    }

    [Fact]
    public void Update_changes_all_mutable_fields()
    {
        var cfg = InsurerConfig.Create(
            AnInsurerId,
            "https://old.com", "old-biz", "old-agent", "old-kv", 10, 1).Value;

        var result = cfg.Update(
            endpointUrl: "https://new.com",
            businessNumber: "new-biz",
            agentCode: "new-agent",
            keyVaultSecretName: "new-kv",
            timeoutSeconds: 60,
            maxRetries: 5);

        result.IsSuccess.Should().BeTrue();
        cfg.EndpointUrl.Should().Be("https://new.com");
        cfg.BusinessNumber.Should().Be("new-biz");
        cfg.AgentCode.Should().Be("new-agent");
        cfg.KeyVaultSecretName.Should().Be("new-kv");
        cfg.TimeoutSeconds.Should().Be(60);
        cfg.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void Update_keeps_previous_state_when_invalid()
    {
        var cfg = InsurerConfig.Create(
            AnInsurerId,
            "https://ok.com", "1", "A", "kv", 30, 3).Value;

        var result = cfg.Update("not-a-url", "1", "A", "kv", 30, 3);

        result.IsSuccess.Should().BeFalse();
        cfg.EndpointUrl.Should().Be("https://ok.com",
            because: "no field changes if any field is invalid");
    }
}
