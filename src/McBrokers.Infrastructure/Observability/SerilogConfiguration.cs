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
    private const string OutputTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {AgentId} {Message:lj}{NewLine}{Exception}";

    public static IHostBuilder UseMcBrokersSerilog(this IHostBuilder builder) =>
        builder.UseSerilog((context, services, lc) =>
        {
            lc.MinimumLevel.Information()
              .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
              .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
              .Enrich.FromLogContext()
              .Enrich.WithMachineName()
              .Enrich.WithEnvironmentName()
              .WriteTo.Console(outputTemplate: OutputTemplate);

            // Sink de archivo con rolling diario. El nombre de archivo deriva del
            // ApplicationName del host (McBrokers.Api / McBrokers.Web) → logs/api-AAAAMMDD.log
            // y logs/web-AAAAMMDD.log. Path relativo al ContentRootPath del proceso.
            var appShort = (context.HostingEnvironment.ApplicationName ?? "app")
                .Replace("McBrokers.", string.Empty, StringComparison.OrdinalIgnoreCase)
                .ToLowerInvariant();
            var logPath = Path.Combine(
                context.HostingEnvironment.ContentRootPath, "..", "..", "logs", $"{appShort}-.log");
            lc.WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                outputTemplate: OutputTemplate,
                retainedFileCountLimit: 14,
                shared: true);

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
