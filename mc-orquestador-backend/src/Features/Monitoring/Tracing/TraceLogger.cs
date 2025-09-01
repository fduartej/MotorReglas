using System.Diagnostics;
using System.Text.Json;

namespace Orchestrator.Features.Monitoring.Tracing;

public class TraceLogger
{
    private readonly ILogger<TraceLogger> _logger;

    public TraceLogger(ILogger<TraceLogger> logger)
    {
        _logger = logger;
    }

    public void LogTrace(
        string traceId,
        string flowName,
        Dictionary<string, object> inputs,
        Dictionary<string, object> datasets,
        Dictionary<string, object> derived,
        EvaluationContext mappingContext,
        Dictionary<string, string> chosenTemplates,
        Dictionary<string, object> payloads,
        AuditConfig auditConfig,
        TimeSpan executionTime)
    {
        if (!auditConfig.Trace.Enabled)
            return;

        try
        {
            var traceData = BuildTraceData(
                traceId, flowName, inputs, datasets, derived, 
                mappingContext, chosenTemplates, payloads, 
                auditConfig.Trace.Fields, executionTime);

            LogTraceData(traceData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging trace for traceId: {TraceId}", traceId);
        }
    }

    public void LogDatasetExecution(
        string traceId,
        string datasetName,
        string datasetType,
        TimeSpan executionTime,
        bool fromCache,
        Exception? error = null)
    {
        var logData = new
        {
            TraceId = traceId,
            DatasetName = datasetName,
            DatasetType = datasetType,
            ExecutionTimeMs = executionTime.TotalMilliseconds,
            FromCache = fromCache,
            Error = error?.Message
        };

        if (error != null)
        {
            _logger.LogError("Dataset execution failed: {@LogData}", logData);
        }
        else
        {
            _logger.LogInformation("Dataset executed: {@LogData}", logData);
        }
    }

    public void LogTemplateSelection(
        string traceId,
        string templateType,
        string selectedTemplate,
        Dictionary<string, string> allOptions,
        TimeSpan executionTime)
    {
        var logData = new
        {
            TraceId = traceId,
            TemplateType = templateType,
            SelectedTemplate = selectedTemplate,
            AllOptions = allOptions,
            ExecutionTimeMs = executionTime.TotalMilliseconds
        };

        _logger.LogInformation("Template selected: {@LogData}", logData);
    }

    public void LogTemplateRendering(
        string traceId,
        string templatePath,
        int payloadSize,
        TimeSpan executionTime,
        Exception? error = null)
    {
        var logData = new
        {
            TraceId = traceId,
            TemplatePath = templatePath,
            PayloadSizeBytes = payloadSize,
            ExecutionTimeMs = executionTime.TotalMilliseconds,
            Error = error?.Message
        };

        if (error != null)
        {
            _logger.LogError("Template rendering failed: {@LogData}", logData);
        }
        else
        {
            _logger.LogInformation("Template rendered: {@LogData}", logData);
        }
    }

    public void LogValidationFailure(
        string traceId,
        string validationType,
        List<string> errors)
    {
        var logData = new
        {
            TraceId = traceId,
            ValidationType = validationType,
            Errors = errors,
            ErrorCount = errors.Count
        };

        _logger.LogWarning("Validation failed: {@LogData}", logData);
    }

    public void LogCacheOperation(
        string traceId,
        string operation,
        string cacheKey,
        bool success,
        TimeSpan? executionTime = null)
    {
        var logData = new
        {
            TraceId = traceId,
            Operation = operation,
            CacheKey = RedactSensitiveData(cacheKey),
            Success = success,
            ExecutionTimeMs = executionTime?.TotalMilliseconds
        };

        _logger.LogDebug("Cache operation: {@LogData}", logData);
    }

    private Dictionary<string, object> BuildTraceData(
        string traceId,
        string flowName,
        Dictionary<string, object> inputs,
        Dictionary<string, object> datasets,
        Dictionary<string, object> derived,
        EvaluationContext mappingContext,
        Dictionary<string, string> chosenTemplates,
        Dictionary<string, object> payloads,
        string[] fields,
        TimeSpan executionTime)
    {
        var traceData = new Dictionary<string, object>
        {
            ["traceId"] = traceId,
            ["flowName"] = flowName,
            ["timestamp"] = DateTime.UtcNow,
            ["executionTimeMs"] = executionTime.TotalMilliseconds
        };

        var fieldsSet = fields.ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (fieldsSet.Contains("inputs"))
        {
            traceData["inputs"] = RedactSensitiveData(inputs);
        }

        if (fieldsSet.Contains("datasets"))
        {
            traceData["datasets"] = RedactSensitiveData(datasets);
        }

        if (fieldsSet.Contains("derived"))
        {
            traceData["derived"] = derived;
        }

        if (fieldsSet.Contains("mappingContext"))
        {
            traceData["mappingContext"] = RedactSensitiveData(mappingContext.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }

        if (fieldsSet.Contains("chosenTemplates"))
        {
            traceData["chosenTemplates"] = chosenTemplates;
        }

        if (fieldsSet.Contains("payloads"))
        {
            traceData["payloads"] = RedactSensitiveData(payloads);
        }

        return traceData;
    }

    private void LogTraceData(Dictionary<string, object> traceData)
    {
        try
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(traceData, jsonOptions);
            _logger.LogInformation("Orchestrator execution trace: {TraceJson}", json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serializing trace data");
            _logger.LogInformation("Orchestrator execution completed - TraceId: {TraceId}, Flow: {FlowName}", 
                traceData.GetValueOrDefault("traceId"), 
                traceData.GetValueOrDefault("flowName"));
        }
    }

    private static object RedactSensitiveData(object data)
    {
        // This is a basic implementation. In production, you'd want more sophisticated
        // redaction based on field names, patterns, etc.
        
        if (data is Dictionary<string, object> dict)
        {
            var redacted = new Dictionary<string, object>();
            foreach (var kvp in dict)
            {
                if (IsSensitiveField(kvp.Key))
                {
                    redacted[kvp.Key] = "[REDACTED]";
                }
                else
                {
                    redacted[kvp.Key] = RedactSensitiveData(kvp.Value);
                }
            }
            return redacted;
        }

        if (data is IEnumerable<object> enumerable && data is not string)
        {
            return enumerable.Select(RedactSensitiveData).ToList();
        }

        return data;
    }

    private static string RedactSensitiveData(string data)
    {
        // Redact potential sensitive data in strings (e.g., cache keys with PII)
        var patterns = new[]
        {
            @"\d{8,}", // Long numbers that might be IDs
            @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", // Email addresses
        };

        var result = data;
        foreach (var pattern in patterns)
        {
            result = System.Text.RegularExpressions.Regex.Replace(result, pattern, "[REDACTED]");
        }

        return result;
    }

    private static bool IsSensitiveField(string fieldName)
    {
        var sensitiveFields = new[]
        {
            "dni", "password", "token", "key", "secret", "auth", 
            "email", "phone", "mobile", "ssn", "account", "credit"
        };

        return sensitiveFields.Any(sensitive => 
            fieldName.Contains(sensitive, StringComparison.OrdinalIgnoreCase));
    }

    public string GenerateTraceId()
    {
        return Activity.Current?.Id ?? Guid.NewGuid().ToString("N")[..16];
    }

    public void StartActivity(string activityName)
    {
        using var activity = Activity.Current?.Source.StartActivity(activityName);
        activity?.SetTag("component", "orchestrator");
    }
}
