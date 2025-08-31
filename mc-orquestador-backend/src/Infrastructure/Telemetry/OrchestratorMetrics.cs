using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Orchestrator.Infrastructure.Telemetry;

/// <summary>
/// Servicio para generar métricas personalizadas del orquestador
/// </summary>
public class OrchestratorMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _flowExecutionCounter;
    private readonly Counter<long> _datasetExecutionCounter;
    private readonly Counter<long> _templateRenderingCounter;
    private readonly Counter<long> _cacheHitCounter;
    private readonly Counter<long> _cacheGissCounter;
    private readonly Histogram<double> _flowExecutionDuration;
    private readonly Histogram<double> _datasetExecutionDuration;
    private readonly Histogram<double> _templateRenderingDuration;
    private readonly Histogram<long> _payloadSize;

    public OrchestratorMetrics()
    {
        _meter = new Meter("Orchestrator.Metrics", "1.0.0");
        
        // Contadores
        _flowExecutionCounter = _meter.CreateCounter<long>(
            "orchestrator_flow_executions_total",
            description: "Total number of flow executions");
            
        _datasetExecutionCounter = _meter.CreateCounter<long>(
            "orchestrator_dataset_executions_total", 
            description: "Total number of dataset executions");
            
        _templateRenderingCounter = _meter.CreateCounter<long>(
            "orchestrator_template_renderings_total",
            description: "Total number of template renderings");
            
        _cacheHitCounter = _meter.CreateCounter<long>(
            "orchestrator_cache_hits_total",
            description: "Total number of cache hits");
            
        _cacheGissCounter = _meter.CreateCounter<long>(
            "orchestrator_cache_misses_total",
            description: "Total number of cache misses");
        
        // Histogramas para duración
        _flowExecutionDuration = _meter.CreateHistogram<double>(
            "orchestrator_flow_execution_duration_seconds",
            unit: "s",
            description: "Duration of flow executions in seconds");
            
        _datasetExecutionDuration = _meter.CreateHistogram<double>(
            "orchestrator_dataset_execution_duration_seconds",
            unit: "s", 
            description: "Duration of dataset executions in seconds");
            
        _templateRenderingDuration = _meter.CreateHistogram<double>(
            "orchestrator_template_rendering_duration_seconds",
            unit: "s",
            description: "Duration of template rendering in seconds");
            
        _payloadSize = _meter.CreateHistogram<long>(
            "orchestrator_payload_size_bytes",
            unit: "bytes",
            description: "Size of generated payloads in bytes");
    }

    /// <summary>
    /// Incrementa el contador de ejecuciones de flow
    /// </summary>
    public void IncrementFlowExecution(string flowName, string status)
    {
        _flowExecutionCounter.Add(1, new KeyValuePair<string, object?>("flow_name", flowName),
                                     new KeyValuePair<string, object?>("status", status));
    }

    /// <summary>
    /// Registra la duración de ejecución de un flow
    /// </summary>
    public void RecordFlowExecutionDuration(string flowName, double durationSeconds, string status)
    {
        _flowExecutionDuration.Record(durationSeconds, 
            new KeyValuePair<string, object?>("flow_name", flowName),
            new KeyValuePair<string, object?>("status", status));
    }

    /// <summary>
    /// Incrementa el contador de ejecuciones de dataset
    /// </summary>
    public void IncrementDatasetExecution(string datasetName, string datasetType, string status)
    {
        _datasetExecutionCounter.Add(1, 
            new KeyValuePair<string, object?>("dataset_name", datasetName),
            new KeyValuePair<string, object?>("dataset_type", datasetType),
            new KeyValuePair<string, object?>("status", status));
    }

    /// <summary>
    /// Registra la duración de ejecución de un dataset
    /// </summary>
    public void RecordDatasetExecutionDuration(string datasetName, string datasetType, double durationSeconds)
    {
        _datasetExecutionDuration.Record(durationSeconds,
            new KeyValuePair<string, object?>("dataset_name", datasetName),
            new KeyValuePair<string, object?>("dataset_type", datasetType));
    }

    /// <summary>
    /// Incrementa el contador de renderizado de templates
    /// </summary>
    public void IncrementTemplateRendering(string templateName, string status)
    {
        _templateRenderingCounter.Add(1,
            new KeyValuePair<string, object?>("template_name", templateName),
            new KeyValuePair<string, object?>("status", status));
    }

    /// <summary>
    /// Registra la duración del renderizado de template
    /// </summary>
    public void RecordTemplateRenderingDuration(string templateName, double durationSeconds)
    {
        _templateRenderingDuration.Record(durationSeconds,
            new KeyValuePair<string, object?>("template_name", templateName));
    }

    /// <summary>
    /// Incrementa el contador de cache hits
    /// </summary>
    public void IncrementCacheHit(string cacheKey, string cacheType)
    {
        _cacheHitCounter.Add(1,
            new KeyValuePair<string, object?>("cache_key", cacheKey),
            new KeyValuePair<string, object?>("cache_type", cacheType));
    }

    /// <summary>
    /// Incrementa el contador de cache misses
    /// </summary>
    public void IncrementCacheMiss(string cacheKey, string cacheType)
    {
        _cacheGissCounter.Add(1,
            new KeyValuePair<string, object?>("cache_key", cacheKey),
            new KeyValuePair<string, object?>("cache_type", cacheType));
    }

    /// <summary>
    /// Registra el tamaño del payload generado
    /// </summary>
    public void RecordPayloadSize(string flowName, long sizeBytes)
    {
        _payloadSize.Record(sizeBytes,
            new KeyValuePair<string, object?>("flow_name", flowName));
    }

    /// <summary>
    /// Incrementa el contador de ejecuciones de endpoint
    /// </summary>
    public void IncrementEndpointExecution(string endpointName, string endpointType, string status)
    {
        _datasetExecutionCounter.Add(1, // Reutilizamos el contador de dataset
            new KeyValuePair<string, object?>("dataset_name", endpointName),
            new KeyValuePair<string, object?>("dataset_type", endpointType),
            new KeyValuePair<string, object?>("status", status));
    }

    /// <summary>
    /// Registra la duración de ejecución de un endpoint
    /// </summary>
    public void RecordEndpointExecutionDuration(string endpointName, double durationSeconds, string status)
    {
        _datasetExecutionDuration.Record(durationSeconds, // Reutilizamos la métrica de dataset
            new KeyValuePair<string, object?>("dataset_name", endpointName),
            new KeyValuePair<string, object?>("dataset_type", "endpoint"),
            new KeyValuePair<string, object?>("status", status));
    }

    public void Dispose()
    {
        _meter?.Dispose();
    }
}
