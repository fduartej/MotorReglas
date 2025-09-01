using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Azure.Monitor.OpenTelemetry.Exporter;

namespace Infrastructure.Telemetry;

public static class OpenTelemetryConfig
{
    public static IServiceCollection AddOpenTelemetryMonitoring(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "MyApp";
        var appInsightsConnectionString = configuration["ApplicationInsights:ConnectionString"];

        if (string.IsNullOrEmpty(appInsightsConnectionString))
        {
            throw new InvalidOperationException("Application Insights ConnectionString is required.");
        }

        // Agregar soporte para Application Insights con OpenTelemetry
        services.AddOpenTelemetry()
            .WithTracing(tracerProviderBuilder =>
            {
                tracerProviderBuilder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation()
                    .AddAzureMonitorTraceExporter(options =>
                    {
                        options.ConnectionString = appInsightsConnectionString;
                    }); // ✅ Exportar trazas a Azure Application Insights
            })
            .WithMetrics(meterProviderBuilder =>
            {
                meterProviderBuilder
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter("MyAppMetrics")
                    .AddAzureMonitorMetricExporter(options =>
                    {
                        options.ConnectionString = appInsightsConnectionString;
                    }); // ✅ Exportar métricas a Azure Application Insights
            });

        return services;
    }
}
