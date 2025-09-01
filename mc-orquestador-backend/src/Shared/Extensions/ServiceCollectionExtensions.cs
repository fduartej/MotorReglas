using Microsoft.Extensions.FileProviders;
using Orchestrator;
using Orchestrator.Features.FlowProcessing.Services;
using Orchestrator.Features.DatasetExecution.Sql;
using Orchestrator.Features.DatasetExecution.Http;
using Orchestrator.Features.PostProcessing.Services;
using Orchestrator.Features.TemplateRendering.Selection;
using Orchestrator.Features.TemplateRendering.Rendering;
using Orchestrator.Features.TemplateRendering.Validation;
using Orchestrator.Features.Monitoring.Tracing;
using Orchestrator.Infrastructure.Cache;
using Orchestrator.Infrastructure.Database;
using Polly;
using Polly.Extensions.Http;
using StackExchange.Redis;
using System.Data;
using Npgsql;

namespace Orchestrator.Shared.Extensions;

public static class Extensions
{
    public static IServiceCollection AddOrchestrator(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind configuration
        var appSettings = configuration.Get<AppSettings>() ?? new AppSettings();
        services.Configure<AppSettings>(configuration);
        services.Configure<ExternalConfig>(configuration.GetSection("ExternalConfig"));
        
        // Override with environment variables
        var flowsPath = Environment.GetEnvironmentVariable("EXTERNAL_FLOWS_PATH") ?? appSettings.ExternalConfig.FlowsPath;
        var templatesPath = Environment.GetEnvironmentVariable("EXTERNAL_TEMPLATES_PATH") ?? appSettings.ExternalConfig.TemplatesPath;
        var cacheSeconds = int.TryParse(Environment.GetEnvironmentVariable("EXTERNAL_CACHE_SECONDS"), out var cs) ? cs : appSettings.ExternalConfig.CacheSeconds;
        
        var externalConfig = new ExternalConfig(flowsPath, templatesPath, cacheSeconds);
        services.AddSingleton(externalConfig);
        
        // File providers - Convert relative paths to absolute
        var absoluteFlowsPath = Path.IsPathRooted(externalConfig.FlowsPath) 
            ? externalConfig.FlowsPath 
            : Path.Combine(Directory.GetCurrentDirectory(), externalConfig.FlowsPath.TrimStart('/'));
            
        var absoluteTemplatesPath = Path.IsPathRooted(externalConfig.TemplatesPath) 
            ? externalConfig.TemplatesPath 
            : Path.Combine(Directory.GetCurrentDirectory(), externalConfig.TemplatesPath.TrimStart('/'));
        
        // Ensure directories exist
        Directory.CreateDirectory(absoluteFlowsPath);
        Directory.CreateDirectory(absoluteTemplatesPath);
        
        services.AddSingleton<IFileProvider>(provider =>
            new PhysicalFileProvider(absoluteFlowsPath));
        
        services.AddKeyedSingleton<IFileProvider>("flows", (provider, key) =>
            new PhysicalFileProvider(absoluteFlowsPath));
            
        services.AddKeyedSingleton<IFileProvider>("templates", (provider, key) =>
            new PhysicalFileProvider(absoluteTemplatesPath));
        
        // Memory cache
        services.AddMemoryCache();
        
        // Redis - Optional connection
        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            try
            {
                var config = configuration.GetSection("Redis").Get<RedisConfig>()?.Configuration ?? "localhost:6379";
                var options = ConfigurationOptions.Parse(config);
                options.AbortOnConnectFail = false;
                options.ConnectTimeout = 1000; // 1 second timeout
                return ConnectionMultiplexer.Connect(options);
            }
            catch (Exception ex)
            {
                var logger = provider.GetService<ILogger<IConnectionMultiplexer>>();
                logger?.LogWarning(ex, "Failed to connect to Redis. Using in-memory cache fallback.");
                
                // Return a mock connection that won't be used
                var options = ConfigurationOptions.Parse("localhost:6379");
                options.AbortOnConnectFail = false;
                return ConnectionMultiplexer.Connect(options);
            }
        });
        
        // Database connections
        services.AddScoped<IConnectionFactory, ConnectionFactory>();
        
        // HTTP Client with basic configuration
        // Note: Polly retry policies are configured per dataset in flow configuration
        var timeoutMs = configuration.GetSection("HttpClients:DefaultRetryPolicy").GetValue("TimeoutMs", 30000);

        services.AddHttpClient<HttpDatasetExecutor>(client =>
        {
            client.Timeout = TimeSpan.FromMilliseconds(timeoutMs);
            client.DefaultRequestHeaders.Add("User-Agent", "Orchestrator/1.0");
        });
        
        // Register services
        services.AddScoped<FlowLoader>();
        services.AddScoped<QueryExecutor>();
        services.AddScoped<HttpDatasetExecutor>();
        services.AddScoped<RedisCache>();
        services.AddScoped<ContextBuilder>();
        services.AddScoped<DerivedEvaluator>();
        services.AddScoped<TemplateSelector>();
        services.AddScoped<TemplateRenderer>();
        services.AddScoped<Validator>();
        services.AddScoped<PostProcessingExecutor>();
        services.AddScoped<TraceLogger>();
        
        // Register telemetry services
        services.AddSingleton<Infrastructure.Telemetry.OrchestratorMetrics>();
        
        return services;
    }
    
    public static T? GetByPath<T>(this Dictionary<string, object> dict, string path)
    {
        if (string.IsNullOrEmpty(path)) return default;
        
        var parts = path.Split('.');
        object? current = dict;
        
        foreach (var part in parts)
        {
            if (current is Dictionary<string, object> d)
            {
                if (d.TryGetValue(part, out var value))
                {
                    current = value;
                }
                else
                {
                    return default;
                }
            }
            else
            {
                return default;
            }
        }
        
        return current is T result ? result : default;
    }
    
    public static void SetByPath(this Dictionary<string, object> dict, string path, object? value)
    {
        if (string.IsNullOrEmpty(path)) return;
        
        var parts = path.Split('.');
        var current = dict;
        
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];
            if (!current.TryGetValue(part, out var nextValue) || nextValue is not Dictionary<string, object>)
            {
                current[part] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }
            current = (Dictionary<string, object>)current[part];
        }
        
        current[parts[^1]] = value!;
    }
}
