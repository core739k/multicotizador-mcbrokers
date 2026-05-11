using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;

namespace McBrokers.Infrastructure.Observability;

/// <summary>
/// Configuración Serilog estructurada para Api y Web. Console sink siempre; Application
/// Insights sink si APPLICATIONINSIGHTS_CONNECTION_STRING está presente en config.
/// </summary>
public static class SerilogConfiguration
{
    public static IHostBuilder UseMcBrokersSerilog(this IHostBuilder builder) =>
        builder.UseSerilog((context, services, lc) =>
        {
            lc.MinimumLevel.Information()
              .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
              .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
              .Enrich.FromLogContext()
              .Enrich.WithMachineName()
              .Enrich.WithEnvironmentName()
              .WriteTo.Console(
                  outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {AgentId} {Message:lj}{NewLine}{Exception}");

            var appInsightsConn = context.Configuration["ApplicationInsights:ConnectionString"]
                ?? context.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

            if (!string.IsNullOrWhiteSpace(appInsightsConn))
            {
                var telemetryConfig = services.GetService<TelemetryConfiguration>();
                if (telemetryConfig is not null)
                {
                    lc.WriteTo.ApplicationInsights(telemetryConfig, TelemetryConverter.Traces);
                }
            }
        });

    public static IServiceCollection AddMcBrokersTelemetry(
        this IServiceCollection services, IConfiguration configuration)
    {
        var conn = configuration["ApplicationInsights:ConnectionString"]
                ?? configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

        if (!string.IsNullOrWhiteSpace(conn))
        {
            services.AddApplicationInsightsTelemetry(options =>
            {
                options.ConnectionString = conn;
            });
        }

        return services;
    }
}
