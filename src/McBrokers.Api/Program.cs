using McBrokers.Api.Endpoints;
using McBrokers.Infrastructure;
using McBrokers.Infrastructure.Observability;
using McBrokers.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseMcBrokersSerilog();

builder.Services.AddMcBrokersTelemetry(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(opts =>
{
    opts.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddMcBrokersInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(
        name: "sql",
        tags: ["ready"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseStatusCodePages();
app.UseMcBrokersCorrelationId();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

app.MapAdminInsurers();
app.MapAdminAgents();
app.MapCatalog();
app.MapQuotations();
app.MapEmissions();

app.Run();

public partial class Program;
