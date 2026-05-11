using System.Net;
using System.Net.Http.Json;
using McBrokers.Api.Tests.Testing;
using McBrokers.Domain.Agents;
using McBrokers.Domain.Insurers;
using McBrokers.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace McBrokers.Api.Tests.Admin;

public class AdminInsurersE2ETests : IClassFixture<AdminApiFactory>
{
    private readonly AdminApiFactory _factory;

    public AdminInsurersE2ETests(AdminApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Full_flow_Create_then_List_writes_auditlog()
    {
        _factory.TestAgentRole = AgentRole.Admin;
        using var client = _factory.CreateClient();

        // 1) Initially empty
        var listBefore = await client.GetAsync("/api/v1/admin/insurers");
        listBefore.StatusCode.Should().Be(HttpStatusCode.OK);
        var bodyBefore = await listBefore.Content.ReadFromJsonAsync<List<InsurerListItem>>();
        bodyBefore!.Should().BeEmpty();

        // 2) Create
        var create = await client.PostAsJsonAsync("/api/v1/admin/insurers",
            new { Code = "Gnp", Name = "Grupo Nacional Provincial", DisplayOrder = 1 });
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        // 3) List has one
        var listAfter = await client.GetAsync("/api/v1/admin/insurers");
        listAfter.StatusCode.Should().Be(HttpStatusCode.OK);
        var bodyAfter = await listAfter.Content.ReadFromJsonAsync<List<InsurerListItem>>();
        bodyAfter!.Should().HaveCount(1);
        bodyAfter[0].Name.Should().Be("Grupo Nacional Provincial");
        bodyAfter[0].Code.Should().Be(nameof(InsurerCode.Gnp));

        // 4) AuditLog has an Insurer.Create entry attributed to test admin
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = db.AuditLog.ToList();
        audit.Should().ContainSingle(a =>
            a.Action == "Insurer.Create"
            && a.EntityType == "Insurer"
            && a.AgentId == _factory.TestAgentId);
    }

    [Fact]
    public async Task Non_admin_is_forbidden_from_admin_endpoints()
    {
        _factory.TestAgentRole = AgentRole.Agent;
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/admin/insurers");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Duplicate_code_returns_400()
    {
        _factory.TestAgentRole = AgentRole.Admin;
        using var client = _factory.CreateClient();

        var first = await client.PostAsJsonAsync("/api/v1/admin/insurers",
            new { Code = "Qua", Name = "Quálitas", DisplayOrder = 1 });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/v1/admin/insurers",
            new { Code = "Qua", Name = "Quálitas duplicada", DisplayOrder = 2 });

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record InsurerListItem(Guid Id, string Code, string Name, bool IsEnabled, int DisplayOrder, string? LogoUrl);
}
