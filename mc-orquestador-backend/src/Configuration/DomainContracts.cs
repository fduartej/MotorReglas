using System.Text.Json.Serialization;
using Orchestrator.Features.PostProcessing.Models;

namespace Orchestrator;

// Configuration Models
public record ExternalConfig(string FlowsPath, string TemplatesPath, int CacheSeconds);

public record RedisConfig(string Configuration);

// Flow Configuration Models
public record FlowConfig(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("inputs")] string[] Inputs,
    [property: JsonPropertyName("settings")] FlowSettings Settings,
    [property: JsonPropertyName("datasets")] List<DatasetConfig> Datasets,
    [property: JsonPropertyName("mapping")] Dictionary<string, string> Mapping,
    [property: JsonPropertyName("collections")] Dictionary<string, CollectionBinding> Collections,
    [property: JsonPropertyName("derived")] Dictionary<string, DerivedItem> Derived,
    [property: JsonPropertyName("templates")] TemplateSection Templates,
    [property: JsonPropertyName("audit")] AuditConfig Audit,
    [property: JsonPropertyName("postProcessing")] PostProcessingConfig? PostProcessing = null
);

public record FlowSettings(bool FailFast, string Timezone);

public record DatasetConfig(
    string Name, 
    string Type, 
    string? Database,
    string? From, 
    List<string>? Select, 
    List<string>? SelectDerived,
    List<JoinConfig>? Joins, 
    List<WhereConfig>? Where, 
    List<string>? OrderBy, 
    int? Limit,
    ResultConfig Result, 
    CacheConfig? Cache,
    string? Sql, 
    Dictionary<string, string>? Params,                 // rawSql
    HttpConfig? Http, 
    Dictionary<string, string>? Extract, 
    List<ResultPath>? ResultPaths, // http
    OnFailureConfig? OnFailure = null
);

public record OnFailureConfig(
    bool Enabled,
    string FallbackType, // "rawSQL"
    FallbackConfig? FallbackConfig
);

public record FallbackConfig(
    string? Sql
);

public record JoinConfig(string Type, string Table, string On);

public record WhereConfig(string Field, string Op, object? Value, string? ValueFrom);

public record ResultConfig(string Mode); // "single" | "array"

public record CacheConfig(bool Enabled, int TtlSec, string Key);

public record HttpConfig(
    string Method, 
    string Url, 
    Dictionary<string, string>? Headers,
    int? TimeoutMs, 
    RetryConfig? Retry, 
    string? BodyTemplate, 
    string? ResultPath
);

public record RetryConfig(int MaxAttempts, int BackoffMs);

public record ResultPath(string As, string Path);

public record CollectionBinding(string From);

public record DerivedItem(SumCollectionField? SumCollectionField, string? Expr);

public record SumCollectionField(string Collection, string Field);

public record TemplateSection(
    TemplateGroup MotorDecision, 
    TemplateGroup Sap
);

public record TemplateGroup(string Default, List<TemplateRule>? Rules);

public record TemplateRule(List<TemplateCond> If, string Template);

public record TemplateCond(string Path, string Op, object? Value);

public record AuditConfig(TraceConfig Trace);

public record TraceConfig(bool Enabled, string[] Fields);

// Input/Output Models
public record PayloadInput(
    string dni, 
    string? cuentaContrato, 
    string? canal, 
    long? transaccionId, 
    string? pais
);

// Internal Models
public record DatasetResult(object Data, bool FromCache = false);

public record TemplateMetadata(List<string>? Required);

public record TemplateInfo(string TemplateName, string Content);

// Expression evaluation context
public class EvaluationContext : Dictionary<string, object>
{
    public EvaluationContext() : base(StringComparer.OrdinalIgnoreCase) { }
    
    public object? GetByPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        
        var parts = path.Split('.');
        object? current = this;
        
        foreach (var part in parts)
        {
            if (current is Dictionary<string, object> dict)
            {
                // Handle array indexing like "items[0]"
                if (part.Contains('[') && part.Contains(']'))
                {
                    var indexStart = part.IndexOf('[');
                    var indexEnd = part.IndexOf(']');
                    var arrayName = part[..indexStart];
                    var indexStr = part[(indexStart + 1)..indexEnd];
                    
                    if (dict.TryGetValue(arrayName, out var arrayValue) && 
                        arrayValue is IList<object> list &&
                        int.TryParse(indexStr, out var index) && 
                        index >= 0 && index < list.Count)
                    {
                        current = list[index];
                    }
                    else
                    {
                        return null;
                    }
                }
                else if (dict.TryGetValue(part, out var value))
                {
                    current = value;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }
        
        return current;
    }
    
    public void SetByPath(string path, object? value)
    {
        if (string.IsNullOrEmpty(path)) return;
        
        var parts = path.Split('.');
        var current = this as Dictionary<string, object>;
        
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];
            if (!current.TryGetValue(part, out var nextValue) || nextValue is not Dictionary<string, object>)
            {
                current[part] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }
            current = (Dictionary<string, object>)current[part]!;
        }
        
        current[parts[^1]] = value!;
    }
}
