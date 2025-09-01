using System.Diagnostics;
using System.Text.Json;
using Orchestrator.Features.PostProcessing.Models;
using Orchestrator.Features.TemplateRendering.Rendering;
using Orchestrator.Features.TemplateRendering.Selection;
using Orchestrator.Features.DatasetExecution.Http;
using Orchestrator.Infrastructure.Telemetry;

namespace Orchestrator.Features.PostProcessing.Services;

/// <summary>
/// Ejecutor de post-procesamiento que maneja las invocaciones a endpoints externos
/// </summary>
public class PostProcessingExecutor
{
    private readonly HttpDatasetExecutor _httpExecutor;
    private readonly TemplateSelector _templateSelector;
    private readonly TemplateRenderer _templateRenderer;
    private readonly OrchestratorMetrics _metrics;
    private readonly ILogger<PostProcessingExecutor> _logger;

    public PostProcessingExecutor(
        HttpDatasetExecutor httpExecutor,
        TemplateSelector templateSelector,
        TemplateRenderer templateRenderer,
        OrchestratorMetrics metrics,
        ILogger<PostProcessingExecutor> logger)
    {
        _httpExecutor = httpExecutor;
        _templateSelector = templateSelector;
        _templateRenderer = templateRenderer;
        _metrics = metrics;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta el post-procesamiento completo
    /// </summary>
    public async Task<PostProcessingResult> ExecuteAsync(
        PostProcessingConfig config, 
        Dictionary<string, object> context,
        string flowName)
    {
        using var activity = OrchestratorActivitySource.Instance.StartActivity($"PostProcessing.{flowName}");
        activity?.SetTag("flow.name", flowName);
        activity?.SetTag("execution.mode", config.ExecutionMode);
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Starting post-processing for flow {FlowName} with mode {ExecutionMode}", 
                flowName, config.ExecutionMode);

            var result = new PostProcessingResult { UpdatedContext = new Dictionary<string, object>(context) };

            switch (config.ExecutionMode.ToLower())
            {
                case "sequential":
                    await ExecuteSequentialAsync(config, result, flowName);
                    break;
                case "conditional":
                    await ExecuteConditionalAsync(config, result, flowName);
                    break;
                case "parallel":
                    await ExecuteParallelAsync(config, result, flowName);
                    break;
                default:
                    throw new ArgumentException($"Unknown execution mode: {config.ExecutionMode}");
            }

            stopwatch.Stop();
            result.TotalElapsedMs = stopwatch.ElapsedMilliseconds;
            result.IsSuccess = !result.Errors.Any();

            _logger.LogInformation("Post-processing completed for flow {FlowName} in {ElapsedMs}ms with {EndpointCount} endpoints", 
                flowName, result.TotalElapsedMs, result.EndpointResults.Count);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error in post-processing for flow {FlowName}", flowName);
            
            return new PostProcessingResult
            {
                IsSuccess = false,
                Errors = { ex.Message },
                TotalElapsedMs = stopwatch.ElapsedMilliseconds,
                UpdatedContext = context
            };
        }
    }

    /// <summary>
    /// Ejecución secuencial
    /// </summary>
    private async Task ExecuteSequentialAsync(
        PostProcessingConfig config, 
        PostProcessingResult result, 
        string flowName)
    {
        var order = config.Order ?? config.Endpoints.Select(e => e.Id).ToList();
        
        foreach (var endpointId in order)
        {
            var endpointConfig = config.Endpoints.FirstOrDefault(e => e.Id == endpointId);
            if (endpointConfig == null)
            {
                _logger.LogWarning("Endpoint {EndpointId} not found in configuration", endpointId);
                continue;
            }

            if (!endpointConfig.Enabled)
            {
                _logger.LogDebug("Endpoint {EndpointId} is disabled, skipping", endpointId);
                continue;
            }

            var endpointResult = await ExecuteEndpointAsync(endpointConfig, result.UpdatedContext, flowName, endpointId);
            result.EndpointResults[endpointId] = endpointResult;

            // Actualizar contexto con los resultados
            if (endpointResult.IsSuccess && endpointResult.MappedData != null)
            {
                foreach (var kvp in endpointResult.MappedData)
                {
                    result.UpdatedContext[kvp.Key] = kvp.Value;
                }
            }

            // Manejar errores según configuración
            if (!endpointResult.IsSuccess && endpointConfig.ErrorHandling?.Strategy == "failFast")
            {
                result.Errors.Add($"Endpoint {endpointId} failed: {endpointResult.Error}");
                break;
            }
        }
    }

    /// <summary>
    /// Ejecución condicional
    /// </summary>
    private async Task ExecuteConditionalAsync(
        PostProcessingConfig config, 
        PostProcessingResult result, 
        string flowName)
    {
        var order = config.Order ?? config.Endpoints.Select(e => e.Id).ToList();
        
        foreach (var endpointId in order)
        {
            var endpointConfig = config.Endpoints.FirstOrDefault(e => e.Id == endpointId);
            if (endpointConfig == null)
                continue;

            if (!endpointConfig.Enabled)
                continue;

            // Evaluar condición si existe
            if (!string.IsNullOrEmpty(endpointConfig.ExecuteIf))
            {
                var shouldExecute = EvaluateCondition(endpointConfig.ExecuteIf, result.UpdatedContext);
                if (!shouldExecute)
                {
                    _logger.LogDebug("Condition not met for endpoint {EndpointId}, skipping", endpointId);
                    continue;
                }
            }

            var endpointResult = await ExecuteEndpointAsync(endpointConfig, result.UpdatedContext, flowName, endpointId);
            result.EndpointResults[endpointId] = endpointResult;

            // Actualizar contexto
            if (endpointResult.IsSuccess && endpointResult.MappedData != null)
            {
                foreach (var kvp in endpointResult.MappedData)
                {
                    result.UpdatedContext[kvp.Key] = kvp.Value;
                }
            }

            // Manejar errores
            if (!endpointResult.IsSuccess && endpointConfig.ErrorHandling?.Strategy == "failFast")
            {
                result.Errors.Add($"Endpoint {endpointId} failed: {endpointResult.Error}");
                break;
            }
        }
    }

    /// <summary>
    /// Ejecución paralela
    /// </summary>
    private async Task ExecuteParallelAsync(
        PostProcessingConfig config, 
        PostProcessingResult result, 
        string flowName)
    {
        var enabledEndpoints = config.Endpoints
            .Where(e => e.Enabled)
            .ToList();

        var tasks = enabledEndpoints.Select(async endpointConfig =>
        {
            return new
            {
                EndpointId = endpointConfig.Id,
                Result = await ExecuteEndpointAsync(endpointConfig, result.UpdatedContext, flowName, endpointConfig.Id)
            };
        });

        var completedTasks = await Task.WhenAll(tasks);

        foreach (var completed in completedTasks)
        {
            result.EndpointResults[completed.EndpointId] = completed.Result;
            
            if (completed.Result.IsSuccess && completed.Result.MappedData != null)
            {
                foreach (var kvp in completed.Result.MappedData)
                {
                    result.UpdatedContext[kvp.Key] = kvp.Value;
                }
            }
            else if (!completed.Result.IsSuccess)
            {
                result.Errors.Add($"Endpoint {completed.EndpointId} failed: {completed.Result.Error}");
            }
        }
    }

    /// <summary>
    /// Ejecuta un endpoint individual
    /// </summary>
    private async Task<EndpointExecutionResult> ExecuteEndpointAsync(
        EndpointInvocationConfig config,
        Dictionary<string, object> context,
        string flowName,
        string endpointName)
    {
        using var activity = OrchestratorActivitySource.Instance.StartActivity($"Endpoint.{endpointName}");
        activity?.SetTag("endpoint.name", endpointName);
        activity?.SetTag("endpoint.type", config.Endpoint?.Type ?? "unknown");
        
        var stopwatch = Stopwatch.StartNew();
        
        var result = new EndpointExecutionResult
        {
            EndpointName = endpointName,
            Status = "executing"
        };

        try
        {
            if (config.Audit?.LogRequest == true)
            {
                _logger.LogInformation("Executing endpoint {EndpointName} for flow {FlowName}", endpointName, flowName);
            }

            // 1. Construir payload según configuración
            object? payload = null;
            string templateUsed = "none";

            if (config.Payload?.UseTemplateResult == true)
            {
                // Usar el template del flujo (motorDecision, sap, etc.)
                var templateSource = config.Payload.TemplateSource ?? "motorDecision";
                
                // Buscar el template renderizado en el contexto
                if (context.TryGetValue($"templates.{templateSource}", out var templateResult))
                {
                    payload = templateResult;
                    templateUsed = templateSource;
                    
                    // Agregar datos adicionales si están configurados
                    if (config.Payload.AdditionalData != null)
                    {
                        var mergedPayload = MergePayloads(templateResult, config.Payload.AdditionalData, context);
                        payload = mergedPayload;
                    }
                }
                else
                {
                    _logger.LogWarning("Template {TemplateSource} not found in context for endpoint {EndpointName}", 
                        templateSource, endpointName);
                    payload = config.Payload.AdditionalData ?? (object)new { error = "Template not found" };
                    templateUsed = $"{templateSource} (not found)";
                }
            }
            else if (config.Payload?.StaticPayload != null)
            {
                // Usar payload estático y renderizar variables
                payload = RenderStaticPayload(config.Payload.StaticPayload, context);
                templateUsed = "static";
            }
            else
            {
                // Payload vacío por defecto
                payload = new { };
                templateUsed = "empty";
            }

            result.TemplateUsed = templateUsed;
            result.RequestPayload = payload;

            if (config.Audit?.LogRequest == true)
            {
                _logger.LogDebug("Endpoint {EndpointName} using template {Template} with payload: {@Payload}", 
                    endpointName, templateUsed, payload);
            }

            // 3. Crear dataset config para HTTP executor
            var datasetConfig = CreateDatasetConfig(config);

            // 4. Ejecutar llamada HTTP
            var response = await _httpExecutor.ExecuteDatasetAsync(datasetConfig, context);
            result.ResponsePayload = response;

            // 5. Mapear respuesta al contexto
            if (config.ResponseMapping != null)
            {
                result.MappedData = MapResponseToContext(response, config.ResponseMapping);
            }

            stopwatch.Stop();
            result.ElapsedMs = stopwatch.ElapsedMilliseconds;
            result.IsSuccess = true;
            result.Status = "success";

            if (config.Audit?.LogResponse == true)
            {
                _logger.LogInformation("Endpoint {EndpointName} completed successfully in {ElapsedMs}ms", 
                    endpointName, result.ElapsedMs);
            }

            // Métricas
            _metrics.IncrementEndpointExecution(endpointName, config.Endpoint?.Type ?? "unknown", "success");
            _metrics.RecordEndpointExecutionDuration(endpointName, result.ElapsedMs / 1000.0, "success");

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.ElapsedMs = stopwatch.ElapsedMilliseconds;
            result.IsSuccess = false;
            result.Status = "error";
            result.Error = ex.Message;

            if (config.Audit?.LogErrors == true)
            {
                _logger.LogError(ex, "Error executing endpoint {EndpointName} after {ElapsedMs}ms", 
                    endpointName, result.ElapsedMs);
            }

            // Métricas
            _metrics.IncrementEndpointExecution(endpointName, config.Endpoint?.Type ?? "unknown", "error");
            _metrics.RecordEndpointExecutionDuration(endpointName, result.ElapsedMs / 1000.0, "error");

            // Aplicar valor por defecto si está configurado
            if (config.ErrorHandling?.DefaultValue != null)
            {
                result.MappedData = new Dictionary<string, object>
                {
                    [endpointName] = config.ErrorHandling.DefaultValue
                };
            }

            return result;
        }
    }

    /// <summary>
    /// Evalúa una condición de string (implementación simple por ahora)
    /// </summary>
    private static bool EvaluateCondition(string condition, Dictionary<string, object> context)
    {
        // Para implementar después: evaluador de expresiones más complejo
        // Por ahora, evaluación simple
        _ = condition; // Evitar warning de parámetro no usado
        _ = context;   // Evitar warning de parámetro no usado
        return true;
    }

    /// <summary>
    /// Crea un DatasetConfig para el HTTP executor
    /// </summary>
    private static DatasetConfig CreateDatasetConfig(EndpointInvocationConfig config)
    {
        // Convertir EndpointInvocationConfig a DatasetConfig
        return new DatasetConfig(
            Name: config.Endpoint?.Name ?? config.Id,
            Type: "http",
            Database: null,
            From: null,
            Select: null,
            SelectDerived: null,
            Joins: null,
            Where: null,
            OrderBy: null,
            Limit: null,
            Result: new ResultConfig("single"),
            Cache: null,
            Sql: null,
            Params: null,
            Http: config.Endpoint?.Http,
            Extract: null,
            ResultPaths: null
        );
    }

    /// <summary>
    /// Mapea la respuesta al contexto usando el responseMapping
    /// </summary>
    private Dictionary<string, object> MapResponseToContext(object response, Dictionary<string, string> mapping)
    {
        var result = new Dictionary<string, object>();
        
        if (response == null) return result;

        var jsonElement = JsonSerializer.SerializeToElement(response);
        
        foreach (var kvp in mapping)
        {
            var contextKey = kvp.Key;
            var responsePath = kvp.Value;
            
            try
            {
                var value = GetValueFromJsonPath(jsonElement, responsePath);
                if (value != null)
                {
                    result[contextKey] = value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to map response path {ResponsePath} to context key {ContextKey}: {Error}", 
                    responsePath, contextKey, ex.Message);
            }
        }

        return result;
    }

    /// <summary>
    /// Combina el resultado del template con datos adicionales
    /// </summary>
    private static object MergePayloads(object templateResult, Dictionary<string, object> additionalData, Dictionary<string, object> context)
    {
        try
        {
            // Renderizar variables en datos adicionales
            var renderedAdditionalData = RenderStaticPayload(additionalData, context);
            
            // Si el template result es un diccionario, combinar
            if (templateResult is Dictionary<string, object> templateDict && 
                renderedAdditionalData is Dictionary<string, object> additionalDict)
            {
                var merged = new Dictionary<string, object>(templateDict);
                foreach (var kvp in additionalDict)
                {
                    merged[kvp.Key] = kvp.Value;
                }
                return merged;
            }
            
            // Si no se pueden combinar, crear un objeto wrapper
            return new
            {
                template = templateResult,
                additional = renderedAdditionalData
            };
        }
        catch (Exception)
        {
            // En caso de error, retornar el template original
            return templateResult;
        }
    }

    /// <summary>
    /// Renderiza un payload estático reemplazando variables del contexto
    /// </summary>
    private static object RenderStaticPayload(Dictionary<string, object> staticPayload, Dictionary<string, object> context)
    {
        var result = new Dictionary<string, object>();
        
        foreach (var kvp in staticPayload)
        {
            result[kvp.Key] = RenderValue(kvp.Value, context);
        }
        
        return result;
    }

    /// <summary>
    /// Renderiza un valor individual reemplazando variables del contexto
    /// </summary>
    private static object RenderValue(object value, Dictionary<string, object> context)
    {
        if (value is string stringValue)
        {
            return RenderStringValue(stringValue, context);
        }
        else if (value is Dictionary<string, object> dictValue)
        {
            return RenderStaticPayload(dictValue, context);
        }
        else if (value is List<object> listValue)
        {
            return listValue.Select(item => RenderValue(item, context)).ToList();
        }
        
        return value;
    }

    /// <summary>
    /// Renderiza un string reemplazando variables con formato {{variable}}
    /// </summary>
    private static string RenderStringValue(string template, Dictionary<string, object> context)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        var result = template;
        
        // Buscar patrones {{variable}} y reemplazar
        var pattern = @"\{\{([^}]+)\}\}";
        var matches = System.Text.RegularExpressions.Regex.Matches(template, pattern);
        
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var variableName = match.Groups[1].Value.Trim();
            if (context.TryGetValue(variableName, out var variableValue))
            {
                var replacement = variableValue?.ToString() ?? "";
                result = result.Replace(match.Value, replacement);
            }
        }
        
        return result;
    }

    /// <summary>
    /// Obtiene un valor del JSON por path
    /// </summary>
    private static object? GetValueFromJsonPath(JsonElement element, string path)
    {
        var current = element;
        var parts = path.Split('.');

        foreach (var part in parts)
        {
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var property))
            {
                current = property;
            }
            else
            {
                return null;
            }
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => current.GetRawText()
        };
    }
}
