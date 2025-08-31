using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Azure.Monitor.OpenTelemetry.Exporter;

namespace Orchestrator.Infrastructure.Telemetry;

public static class OpenTelemetryConfig
{
    public static IServiceCollection AddOpenTelemetryMonitoring(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "mc-orquestador-backend";
        var appInsightsConnectionString = configuration["ApplicationInsights:ConnectionString"];

        if (string.IsNullOrEmpty(appInsightsConnectionString))
        {
            // En desarrollo, OpenTelemetry puede funcionar sin Application Insights
            services.AddOpenTelemetry()
                .WithTracing(tracerProviderBuilder =>
                {
                    tracerProviderBuilder
                        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
                        .AddAspNetCoreInstrumentation(options =>
                        {
                            options.RecordException = true;
                        })
                        .AddHttpClientInstrumentation(options =>
                        {
                            options.RecordException = true;
                        })
                        .AddSource("Orchestrator.*") // Agregar instrumentación personalizada del orquestador
                        .AddConsoleExporter(); // Exportar a consola en desarrollo
                })
                .WithMetrics(meterProviderBuilder =>
                {
                    meterProviderBuilder
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddMeter("Orchestrator.Metrics") // Métricas personalizadas del orquestador
                        .AddConsoleExporter(); // Exportar a consola en desarrollo
                });

            return services;
        }

        // Configuración completa con Azure Application Insights
        services.AddOpenTelemetry()
            .WithTracing(tracerProviderBuilder =>
            {
                tracerProviderBuilder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(serviceName)
                        .AddAttributes(new Dictionary<string, object>
                        {
                            ["service.namespace"] = "MotorReglas",
                            ["service.instance.id"] = Environment.MachineName,
                            ["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
                        }))
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = (httpContext) =>
                        {
                            // Filtrar health checks para reducir ruido
                            return !httpContext.Request.Path.Value?.Contains("/health") == true;
                        };
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.FilterHttpRequestMessage = (httpRequestMessage) =>
                        {
                            // Instrumentar todas las llamadas HTTP salientes
                            return true;
                        };
                    })
                    .AddSource("Orchestrator.*") // Instrumentación personalizada
                    .AddSource("FlowProcessing.*") // Instrumentación de procesamiento de flows
                    .AddSource("DatasetExecution.*") // Instrumentación de ejecución de datasets
                    .AddSource("TemplateRendering.*") // Instrumentación de renderizado de templates
                    .AddAzureMonitorTraceExporter(options =>
                    {
                        options.ConnectionString = appInsightsConnectionString;
                    });
            })
            .WithMetrics(meterProviderBuilder =>
            {
                meterProviderBuilder
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation() // Métricas de runtime .NET
                    .AddMeter("Orchestrator.Metrics") // Métricas personalizadas
                    .AddMeter("FlowProcessing.Metrics") // Métricas de flows
                    .AddMeter("DatasetExecution.Metrics") // Métricas de datasets
                    .AddMeter("TemplateRendering.Metrics") // Métricas de templates
                    .AddAzureMonitorMetricExporter(options =>
                    {
                        options.ConnectionString = appInsightsConnectionString;
                    });
            });

        return services;
    }
}
