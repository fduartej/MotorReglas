using Orchestrator;
using Orchestrator.Features.DatasetExecution.Http;
using Orchestrator.Features.DatasetExecution.Sql;
using Orchestrator.Features.FlowProcessing.Interfaces;
using Orchestrator.Features.FlowProcessing.Models;
using Orchestrator.Features.Monitoring.Tracing;
using Orchestrator.Features.PostProcessing.Services;
using Orchestrator.Features.TemplateRendering.Rendering;
using Orchestrator.Features.TemplateRendering.Selection;
using Orchestrator.Features.TemplateRendering.Validation;
using Orchestrator.Infrastructure.Cache;
using System.Diagnostics;

namespace Orchestrator.Features.FlowProcessing.Services;

public class FlowOrchestrator : IFlowOrchestrator
{
    private readonly FlowLoader _flowLoader;
    private readonly QueryExecutor _queryExecutor;
    private readonly HttpDatasetExecutor _httpExecutor;
    private readonly RedisCache _cache;
    private readonly ContextBuilder _contextBuilder;
    private readonly DerivedEvaluator _derivedEvaluator;
    private readonly TemplateSelector _templateSelector;
    private readonly TemplateRenderer _templateRenderer;
    private readonly Validator _validator;
    private readonly PostProcessingExecutor _postProcessingExecutor;
    private readonly TraceLogger _traceLogger;
    private readonly ILogger<FlowOrchestrator> _logger;

    public FlowOrchestrator(
        FlowLoader flowLoader,
        QueryExecutor queryExecutor,
        HttpDatasetExecutor httpExecutor,
        RedisCache cache,
        ContextBuilder contextBuilder,
        DerivedEvaluator derivedEvaluator,
        TemplateSelector templateSelector,
        TemplateRenderer templateRenderer,
        Validator validator,
        PostProcessingExecutor postProcessingExecutor,
        TraceLogger traceLogger,
        ILogger<FlowOrchestrator> logger)
    {
        _flowLoader = flowLoader;
        _queryExecutor = queryExecutor;
        _httpExecutor = httpExecutor;
        _cache = cache;
        _contextBuilder = contextBuilder;
        _derivedEvaluator = derivedEvaluator;
        _templateSelector = templateSelector;
        _templateRenderer = templateRenderer;
        _validator = validator;
        _postProcessingExecutor = postProcessingExecutor;
        _traceLogger = traceLogger;
        _logger = logger;
    }

    public async Task<OrchestratorResponse> ExecuteFlowAsync(string flowName, Dictionary<string, object> inputData)
    {
        var traceId = _traceLogger.GenerateTraceId();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Processing flow {FlowName} with traceId {TraceId}", flowName, traceId);

            // Load flow configuration
            var flowConfig = await _flowLoader.LoadFlowAsync(flowName);
            if (flowConfig == null)
            {
                _logger.LogWarning("Flow {FlowName} not found", flowName);
                return new OrchestratorResponse
                {
                    IsSuccess = false,
                    Error = $"Flow '{flowName}' not found",
                    Debug = new DebugInfo { TraceId = traceId, ElapsedMs = stopwatch.ElapsedMilliseconds }
                };
            }

            // Start building context with inputs - simplified call
            var context = _contextBuilder.BuildContext(inputData, new Dictionary<string, object>(), flowConfig);
            
            // Process datasets in parallel
            var datasetResults = new Dictionary<string, object>();
            
            _logger.LogInformation("Starting parallel execution of {DatasetCount} datasets for flow {FlowName}", 
                flowConfig.Datasets.Count, flowName);
            
            var datasetTasks = flowConfig.Datasets.Select(async dataset =>
            {
                var datasetStopwatch = Stopwatch.StartNew();
                try
                {
                    _logger.LogDebug("Starting dataset {DatasetName} of type {DatasetType}", 
                        dataset.Name, dataset.Type);
                    
                    object? result = null;
                    
                    // Check cache first
                    if (dataset.Cache?.Enabled == true)
                    {
                        var cacheKey = _cache.BuildCacheKey(dataset.Cache.Key, inputData);
                        result = await _cache.GetAsync<object>(cacheKey);
                        
                        if (result != null)
                        {
                            _logger.LogDebug("Cache hit for dataset {DatasetName}", dataset.Name);
                        }
                    }
                    
                    // Execute dataset if not cached
                    if (result == null)
                    {
                        result = dataset.Type.ToLower() switch
                        {
                            "sql" or "rawsql" => await _queryExecutor.ExecuteDatasetAsync(dataset, context),
                            "http" => await _httpExecutor.ExecuteDatasetAsync(dataset, context),
                            _ => throw new ArgumentException($"Unsupported dataset type: {dataset.Type}")
                        };
                        
                        // Cache result if enabled
                        if (dataset.Cache?.Enabled == true && result != null)
                        {
                            var cacheKey = _cache.BuildCacheKey(dataset.Cache.Key, inputData);
                            await _cache.SetAsync(cacheKey, result, TimeSpan.FromSeconds(dataset.Cache.TtlSec));
                        }
                    }
                    
                    datasetStopwatch.Stop();
                    _logger.LogDebug("Dataset {DatasetName} completed in {ElapsedMs}ms", 
                        dataset.Name, datasetStopwatch.ElapsedMilliseconds);
                    
                    return new { DatasetName = dataset.Name, Result = result };
                }
                catch (Exception ex)
                {
                    datasetStopwatch.Stop();
                    _logger.LogError(ex, "Error processing dataset {DatasetName} after {ElapsedMs}ms", 
                        dataset.Name, datasetStopwatch.ElapsedMilliseconds);
                    return new { DatasetName = dataset.Name, Result = (object?)new { error = ex.Message } };
                }
            });
            
            // Wait for all datasets to complete
            var completedDatasets = await Task.WhenAll(datasetTasks);
            
            _logger.LogInformation("All {DatasetCount} datasets completed for flow {FlowName}", 
                flowConfig.Datasets.Count, flowName);
            
            // Collect results
            foreach (var completedDataset in completedDatasets)
            {
                if (completedDataset.Result != null)
                {
                    datasetResults[completedDataset.DatasetName] = completedDataset.Result;
                }
            }
            
            // Apply mapping and derived fields  
            var finalContext = _contextBuilder.BuildContext(inputData, datasetResults, flowConfig);
            var derivedResults = _derivedEvaluator.EvaluateDerived(finalContext, flowConfig.Derived);
            
            // Add derived results to context
            foreach (var derived in derivedResults)
            {
                finalContext[derived.Key] = derived.Value;
            }
            
            // Execute post-processing if configured
            var postProcessingResults = new Dictionary<string, object>();
            if (flowConfig.PostProcessing != null)
            {
                _logger.LogInformation("Starting post-processing for flow {FlowName}", flowName);
                
                var postProcessingResult = await _postProcessingExecutor.ExecuteAsync(
                    flowConfig.PostProcessing, 
                    finalContext.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), 
                    flowName);
                
                if (postProcessingResult.IsSuccess)
                {
                    // Update context with post-processing results
                    finalContext.Clear();
                    foreach (var kvp in postProcessingResult.UpdatedContext)
                    {
                        finalContext[kvp.Key] = kvp.Value;
                    }
                    postProcessingResults = postProcessingResult.EndpointResults.ToDictionary(
                        kvp => kvp.Key, 
                        kvp => (object)kvp.Value);
                        
                    _logger.LogInformation("Post-processing completed successfully with {EndpointCount} endpoints", 
                        postProcessingResult.EndpointResults.Count);
                }
                else
                {
                    _logger.LogError("Post-processing failed: {Errors}", 
                        string.Join(", ", postProcessingResult.Errors));
                }
            }
            
            stopwatch.Stop();
            
            _logger.LogInformation("Flow {FlowName} completed in {ElapsedMs}ms with traceId {TraceId}", 
                flowName, stopwatch.ElapsedMilliseconds, traceId);
            
            // Determine final template name for response
            var finalTemplate = postProcessingResults.Any() ? "postProcessing" : "legacy";
            
            return new OrchestratorResponse
            {
                IsSuccess = true,
                Template = finalTemplate,
                Payload = postProcessingResults.Any() ? postProcessingResults : (object)finalContext,
                Debug = new DebugInfo
                {
                    TraceId = traceId,
                    ElapsedMs = stopwatch.ElapsedMilliseconds,
                    Datasets = datasetResults,
                    InputData = inputData,
                    Context = finalContext,
                    PostProcessingResults = postProcessingResults
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error processing flow {FlowName} with traceId {TraceId}", flowName, traceId);
            
            return new OrchestratorResponse
            {
                IsSuccess = false,
                Error = ex.Message,
                Debug = new DebugInfo
                {
                    TraceId = traceId,
                    ElapsedMs = stopwatch.ElapsedMilliseconds
                }
            };
        }
    }

    public async Task<object?> GetFlowConfigAsync(string flowName)
    {
        return await _flowLoader.LoadFlowAsync(flowName);
    }

    public async Task<List<string>> GetAvailableFlowsAsync()
    {
        // This would require extending FlowLoader to list available flows
        await Task.Delay(1); // Placeholder
        return new List<string> { "FNB" }; // Hardcoded for now
    }

    public async Task ClearCacheAsync(string key)
    {
        await _cache.RemoveAsync(key);
    }
}
