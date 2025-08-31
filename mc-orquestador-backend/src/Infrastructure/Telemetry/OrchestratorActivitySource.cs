using System.Diagnostics;

namespace Orchestrator.Infrastructure.Telemetry;

/// <summary>
/// ActivitySource para generar trazas personalizadas del orquestador
/// </summary>
public static class OrchestratorActivitySource
{
    public static readonly ActivitySource Instance = new("Orchestrator.Main", "1.0.0");
    
    /// <summary>
    /// Crea una actividad para la ejecución de un flow
    /// </summary>
    public static Activity? StartFlowExecution(string flowName)
    {
        var activity = Instance.StartActivity($"Flow.Execute.{flowName}");
        activity?.SetTag("flow.name", flowName);
        activity?.SetTag("orchestrator.component", "flow-processing");
        return activity;
    }
    
    /// <summary>
    /// Crea una actividad para la ejecución de un dataset
    /// </summary>
    public static Activity? StartDatasetExecution(string datasetName, string datasetType)
    {
        var activity = Instance.StartActivity($"Dataset.Execute.{datasetName}");
        activity?.SetTag("dataset.name", datasetName);
        activity?.SetTag("dataset.type", datasetType);
        activity?.SetTag("orchestrator.component", "dataset-execution");
        return activity;
    }
    
    /// <summary>
    /// Crea una actividad para el renderizado de un template
    /// </summary>
    public static Activity? StartTemplateRendering(string templateName)
    {
        var activity = Instance.StartActivity($"Template.Render.{templateName}");
        activity?.SetTag("template.name", templateName);
        activity?.SetTag("orchestrator.component", "template-rendering");
        return activity;
    }
    
    /// <summary>
    /// Crea una actividad para operaciones de cache
    /// </summary>
    public static Activity? StartCacheOperation(string operation, string cacheKey)
    {
        var activity = Instance.StartActivity($"Cache.{operation}");
        activity?.SetTag("cache.operation", operation);
        activity?.SetTag("cache.key", cacheKey);
        activity?.SetTag("orchestrator.component", "cache");
        return activity;
    }
    
    /// <summary>
    /// Agrega información de contexto a la actividad actual
    /// </summary>
    public static void AddContextInfo(this Activity? activity, Dictionary<string, object> context)
    {
        if (activity == null) return;
        
        foreach (var kvp in context.Take(10)) // Limitar a 10 para evitar overhead
        {
            activity.SetTag($"context.{kvp.Key}", kvp.Value?.ToString());
        }
    }
    
    /// <summary>
    /// Marca la actividad como exitosa
    /// </summary>
    public static void SetSuccess(this Activity? activity)
    {
        activity?.SetTag("orchestrator.status", "success");
        activity?.SetStatus(ActivityStatusCode.Ok);
    }
    
    /// <summary>
    /// Marca la actividad como fallida
    /// </summary>
    public static void SetError(this Activity? activity, Exception exception)
    {
        activity?.SetTag("orchestrator.status", "error");
        activity?.SetTag("error.type", exception.GetType().Name);
        activity?.SetTag("error.message", exception.Message);
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
    }
}
