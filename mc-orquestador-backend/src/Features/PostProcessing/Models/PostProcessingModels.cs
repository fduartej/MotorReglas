using System.Text.Json.Serialization;

namespace Orchestrator.Features.PostProcessing.Models;

/// <summary>
/// Configuración de post-procesamiento para un flujo
/// </summary>
public class PostProcessingConfig
{
    /// <summary>
    /// Modo de ejecución: sequential, conditional, parallel
    /// </summary>
    [JsonPropertyName("executionMode")]
    public string ExecutionMode { get; set; } = "sequential";

    /// <summary>
    /// Orden de ejecución para modo secuencial
    /// </summary>
    [JsonPropertyName("order")]
    public List<string>? Order { get; set; }

    /// <summary>
    /// Configuración de endpoints a invocar
    /// </summary>
    [JsonPropertyName("endpoints")]
    public List<EndpointInvocationConfig> Endpoints { get; set; } = new();
}

/// <summary>
/// Configuración de invocación de un endpoint específico
/// </summary>
public class EndpointInvocationConfig
{
    /// <summary>
    /// ID único del endpoint
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// Nombre descriptivo del endpoint
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Si está habilitado
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Condición para ejecutar (solo en modo conditional)
    /// </summary>
    [JsonPropertyName("executeIf")]
    public string? ExecuteIf { get; set; }

    /// <summary>
    /// Configuración del endpoint HTTP
    /// </summary>
    [JsonPropertyName("endpoint")]
    public EndpointConfig? Endpoint { get; set; }

    /// <summary>
    /// Configuración del payload y templates
    /// </summary>
    [JsonPropertyName("payload")]
    public PayloadConfig? Payload { get; set; }

    /// <summary>
    /// Mapeo de la respuesta al contexto
    /// </summary>
    [JsonPropertyName("responseMapping")]
    public Dictionary<string, string>? ResponseMapping { get; set; }

    /// <summary>
    /// Configuración de manejo de errores
    /// </summary>
    [JsonPropertyName("errorHandling")]
    public ErrorHandlingConfig? ErrorHandling { get; set; }

    /// <summary>
    /// Configuración de auditoría
    /// </summary>
    [JsonPropertyName("audit")]
    public AuditConfig? Audit { get; set; }
}

/// <summary>
/// Configuración del endpoint HTTP
/// </summary>
public class EndpointConfig
{
    /// <summary>
    /// Nombre del endpoint
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Tipo (siempre "http" por ahora)
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "http";

    /// <summary>
    /// Configuración HTTP
    /// </summary>
    [JsonPropertyName("http")]
    public HttpConfig? Http { get; set; }
}

/// <summary>
/// Configuración de payload y template
/// </summary>
public class PayloadConfig
{
    /// <summary>
    /// Si usar el resultado del template armado en el flujo
    /// </summary>
    [JsonPropertyName("useTemplateResult")]
    public bool UseTemplateResult { get; set; } = false;

    /// <summary>
    /// Fuente del template del flujo (motorDecision, sap, etc.)
    /// </summary>
    [JsonPropertyName("templateSource")]
    public string? TemplateSource { get; set; }

    /// <summary>
    /// Estrategia de merge con datos adicionales
    /// </summary>
    [JsonPropertyName("mergeStrategy")]
    public string? MergeStrategy { get; set; } = "templateFirst";

    /// <summary>
    /// Datos adicionales para agregar al payload
    /// </summary>
    [JsonPropertyName("additionalData")]
    public Dictionary<string, object>? AdditionalData { get; set; }

    /// <summary>
    /// Payload estático (cuando no se usa template)
    /// </summary>
    [JsonPropertyName("staticPayload")]
    public Dictionary<string, object>? StaticPayload { get; set; }

    /// <summary>
    /// Template por defecto (para compatibilidad futura)
    /// </summary>
    [JsonPropertyName("templateFile")]
    public string? TemplateFile { get; set; }

    /// <summary>
    /// Reglas para seleccionar templates alternativos
    /// </summary>
    [JsonPropertyName("rules")]
    public List<TemplateRule>? Rules { get; set; }
}

/// <summary>
/// Regla para selección de template
/// </summary>
public class TemplateRule
{
    /// <summary>
    /// Condiciones para aplicar esta regla
    /// </summary>
    [JsonPropertyName("if")]
    public required List<ConditionConfig> If { get; set; }

    /// <summary>
    /// Template a usar si se cumple la condición
    /// </summary>
    [JsonPropertyName("templateFile")]
    public required string TemplateFile { get; set; }
}

/// <summary>
/// Configuración de condición
/// </summary>
public class ConditionConfig
{
    /// <summary>
    /// Ruta del campo a evaluar
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; set; }

    /// <summary>
    /// Operador de comparación
    /// </summary>
    [JsonPropertyName("op")]
    public required string Op { get; set; }

    /// <summary>
    /// Valor a comparar
    /// </summary>
    [JsonPropertyName("value")]
    public object? Value { get; set; }
}

/// <summary>
/// Configuración de manejo de errores
/// </summary>
public class ErrorHandlingConfig
{
    /// <summary>
    /// Estrategia: failFast, continue, retry
    /// </summary>
    [JsonPropertyName("strategy")]
    public string Strategy { get; set; } = "failFast";

    /// <summary>
    /// Número máximo de reintentos
    /// </summary>
    [JsonPropertyName("maxRetries")]
    public int MaxRetries { get; set; } = 0;

    /// <summary>
    /// Tiempo de espera entre reintentos (ms)
    /// </summary>
    [JsonPropertyName("retryDelayMs")]
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Valor por defecto en caso de error
    /// </summary>
    [JsonPropertyName("defaultValue")]
    public object? DefaultValue { get; set; }
}

/// <summary>
/// Configuración de auditoría
/// </summary>
public class AuditConfig
{
    /// <summary>
    /// Loggear request
    /// </summary>
    [JsonPropertyName("logRequest")]
    public bool LogRequest { get; set; } = true;

    /// <summary>
    /// Loggear response
    /// </summary>
    [JsonPropertyName("logResponse")]
    public bool LogResponse { get; set; } = true;

    /// <summary>
    /// Incluir en trace de OpenTelemetry
    /// </summary>
    [JsonPropertyName("includeInTrace")]
    public bool IncludeInTrace { get; set; } = true;

    /// <summary>
    /// Loggear errores
    /// </summary>
    [JsonPropertyName("logErrors")]
    public bool LogErrors { get; set; } = true;
}

/// <summary>
/// Resultado de ejecución de un endpoint
/// </summary>
public class EndpointExecutionResult
{
    /// <summary>
    /// Nombre del endpoint
    /// </summary>
    public required string EndpointName { get; set; }

    /// <summary>
    /// Si fue exitoso
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Template usado
    /// </summary>
    public string? TemplateUsed { get; set; }

    /// <summary>
    /// Payload enviado
    /// </summary>
    public object? RequestPayload { get; set; }

    /// <summary>
    /// Respuesta recibida
    /// </summary>
    public object? ResponsePayload { get; set; }

    /// <summary>
    /// Tiempo de ejecución en ms
    /// </summary>
    public long ElapsedMs { get; set; }

    /// <summary>
    /// Estado de la ejecución
    /// </summary>
    public string Status { get; set; } = "unknown";

    /// <summary>
    /// Error si ocurrió
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Datos mapeados al contexto
    /// </summary>
    public Dictionary<string, object>? MappedData { get; set; }
}

/// <summary>
/// Resultado de todo el post-procesamiento
/// </summary>
public class PostProcessingResult
{
    /// <summary>
    /// Si todo fue exitoso
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Resultados individuales por endpoint
    /// </summary>
    public Dictionary<string, EndpointExecutionResult> EndpointResults { get; set; } = new();

    /// <summary>
    /// Tiempo total de ejecución
    /// </summary>
    public long TotalElapsedMs { get; set; }

    /// <summary>
    /// Errores generales
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Contexto actualizado con las respuestas
    /// </summary>
    public Dictionary<string, object> UpdatedContext { get; set; } = new();
}
