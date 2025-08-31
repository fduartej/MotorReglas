using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System.Text.Json;
using Orchestrator;

namespace Orchestrator.Features.FlowProcessing.Services;

public class FlowLoader
{
    private readonly IFileProvider _fileProvider;
    private readonly IMemoryCache _cache;
    private readonly ExternalConfig _config;
    private readonly ILogger<FlowLoader> _logger;

    public FlowLoader(
        [FromKeyedServices("flows")] IFileProvider fileProvider,
        IMemoryCache cache,
        ExternalConfig config,
        ILogger<FlowLoader> logger)
    {
        _fileProvider = fileProvider;
        _cache = cache;
        _config = config;
        _logger = logger;
    }

    public async Task<FlowConfig?> LoadFlowAsync(string flowName)
    {
        var cacheKey = $"flow:{flowName}";
        
        if (_cache.TryGetValue(cacheKey, out FlowConfig? cachedFlow))
        {
            _logger.LogDebug("Flow {FlowName} loaded from cache", flowName);
            return cachedFlow;
        }

        var fileName = $"flow-{flowName}.json";
        var fileInfo = _fileProvider.GetFileInfo(fileName);
        
        if (!fileInfo.Exists)
        {
            _logger.LogWarning("Flow file {FileName} not found", fileName);
            return null;
        }

        try
        {
            var content = await ReadFileContentAsync(fileInfo);
            var flow = JsonSerializer.Deserialize<FlowConfig>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (flow == null)
            {
                _logger.LogError("Failed to deserialize flow {FlowName}", flowName);
                return null;
            }

            // Validate flow
            if (!ValidateFlow(flow))
            {
                _logger.LogError("Flow {FlowName} validation failed", flowName);
                return null;
            }

            // Cache with file change token
            var changeToken = _fileProvider.Watch(fileName);
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(_config.CacheSeconds))
                .AddExpirationToken(changeToken)
                .SetPriority(CacheItemPriority.High);

            _cache.Set(cacheKey, flow, cacheOptions);
            
            _logger.LogInformation("Flow {FlowName} loaded and cached", flowName);
            return flow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading flow {FlowName}", flowName);
            return null;
        }
    }

    private static async Task<string> ReadFileContentAsync(IFileInfo fileInfo)
    {
        using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private bool ValidateFlow(FlowConfig flow)
    {
        if (string.IsNullOrEmpty(flow.Name))
        {
            _logger.LogError("Flow name is required");
            return false;
        }

        if (flow.Inputs == null || flow.Inputs.Length == 0)
        {
            _logger.LogError("Flow inputs are required");
            return false;
        }

        if (flow.Datasets == null || flow.Datasets.Count == 0)
        {
            _logger.LogError("Flow must have at least one dataset");
            return false;
        }

        // Validate dataset configurations
        foreach (var dataset in flow.Datasets)
        {
            if (string.IsNullOrEmpty(dataset.Name))
            {
                _logger.LogError("Dataset name is required");
                return false;
            }

            if (string.IsNullOrEmpty(dataset.Type))
            {
                _logger.LogError("Dataset type is required");
                return false;
            }

            if (!IsValidDatasetType(dataset.Type))
            {
                _logger.LogError("Invalid dataset type: {Type}", dataset.Type);
                return false;
            }

            // Type-specific validation
            switch (dataset.Type.ToLower())
            {
                case "sql":
                    if (string.IsNullOrEmpty(dataset.Database) || string.IsNullOrEmpty(dataset.From))
                    {
                        _logger.LogError("SQL dataset requires Database and From");
                        return false;
                    }
                    break;
                case "rawsql":
                    if (string.IsNullOrEmpty(dataset.Database) || string.IsNullOrEmpty(dataset.Sql))
                    {
                        _logger.LogError("RawSQL dataset requires Database and Sql");
                        return false;
                    }
                    break;
                case "http":
                    if (dataset.Http == null || string.IsNullOrEmpty(dataset.Http.Url))
                    {
                        _logger.LogError("HTTP dataset requires Http configuration with Url");
                        return false;
                    }
                    break;
            }
        }

        return true;
    }

    private static bool IsValidDatasetType(string type)
    {
        return type.ToLower() switch
        {
            "sql" or "rawsql" or "http" => true,
            _ => false
        };
    }
}
