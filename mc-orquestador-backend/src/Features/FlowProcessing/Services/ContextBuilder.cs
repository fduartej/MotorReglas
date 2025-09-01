namespace Orchestrator.Features.FlowProcessing.Services;

using Orchestrator;

public class ContextBuilder
{
    private readonly ILogger<ContextBuilder> _logger;

    public ContextBuilder(ILogger<ContextBuilder> logger)
    {
        _logger = logger;
    }

    public EvaluationContext BuildContext(
        Dictionary<string, object> inputs,
        Dictionary<string, object> datasetResults,
        FlowConfig flow)
    {
        var context = new EvaluationContext();

        // Add inputs to context
        foreach (var input in inputs)
        {
            context[input.Key] = input.Value;
        }

        // Add datasets to context
        context["datasets"] = datasetResults;

        // Apply mapping
        ApplyMapping(context, datasetResults, flow.Mapping);

        // Bind collections
        BindCollections(context, datasetResults, flow.Collections);

        _logger.LogDebug("Built context with {InputCount} inputs, {DatasetCount} datasets, {MappingCount} mappings, {CollectionCount} collections",
            inputs.Count, datasetResults.Count, flow.Mapping?.Count ?? 0, flow.Collections?.Count ?? 0);

        return context;
    }

    private void ApplyMapping(EvaluationContext context, Dictionary<string, object> datasetResults, Dictionary<string, string>? mapping)
    {
        if (mapping == null || mapping.Count == 0)
            return;

        foreach (var map in mapping)
        {
            var targetPath = map.Key;
            var sourcePath = map.Value;

            try
            {
                var value = GetValueByPath(datasetResults, sourcePath);
                context.SetByPath(targetPath, value);
                
                _logger.LogTrace("Mapped {SourcePath} to {TargetPath}: {Value}", sourcePath, targetPath, value);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error mapping {SourcePath} to {TargetPath}", sourcePath, targetPath);
            }
        }
    }

    private static void BindCollections(EvaluationContext context, Dictionary<string, object> datasetResults, Dictionary<string, CollectionBinding>? collections)
    {
        if (collections == null || collections.Count == 0)
            return;

        foreach (var collection in collections)
        {
            var collectionName = collection.Key;
            var binding = collection.Value;

            if (datasetResults.TryGetValue(binding.From, out var datasetResult))
            {
                context[collectionName] = datasetResult;
            }
        }
    }

    private static object? GetValueByPath(Dictionary<string, object> data, string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        var parts = path.Split('.');
        object? current = data;

        foreach (var part in parts)
        {
            if (current == null)
                return null;

            // Handle array indexing
            if (part.Contains('[') && part.Contains(']'))
            {
                var indexStart = part.IndexOf('[');
                var indexEnd = part.IndexOf(']');
                var arrayName = part[..indexStart];
                var indexStr = part[(indexStart + 1)..indexEnd];

                if (current is Dictionary<string, object> dict && dict.TryGetValue(arrayName, out var arrayValue))
                {
                    if (arrayValue is IList<object> list && int.TryParse(indexStr, out var index))
                    {
                        current = index >= 0 && index < list.Count ? list[index] : null;
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
            else if (current is Dictionary<string, object> dict)
            {
                // Handle special properties
                if (part == "length" || part == "size")
                {
                    if (dict.Values.FirstOrDefault() is ICollection<object> collection)
                    {
                        return collection.Count;
                    }
                    return dict.Count;
                }

                dict.TryGetValue(part, out current);
            }
            else if (current is IList<object> list)
            {
                if (part == "length" || part == "size")
                {
                    return list.Count;
                }
                
                if (int.TryParse(part, out var index))
                {
                    current = index >= 0 && index < list.Count ? list[index] : null;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                // Try to get property via reflection for dynamic objects
                try
                {
                    var property = current.GetType().GetProperty(part);
                    if (property != null && property.CanRead)
                    {
                        current = property.GetValue(current);
                    }
                    else
                    {
                        return null;
                    }
                }
                catch
                {
                    return null;
                }
            }
        }

        return current;
    }

    public void AddDerivedFields(EvaluationContext context, Dictionary<string, object> derivedResults)
    {
        foreach (var derived in derivedResults)
        {
            context.SetByPath(derived.Key, derived.Value);
        }
        
        _logger.LogDebug("Added {Count} derived fields to context", derivedResults.Count);
    }

    public Dictionary<string, object> ExtractDatasets(EvaluationContext context)
    {
        return context.TryGetValue("datasets", out var datasets) && datasets is Dictionary<string, object> dict 
            ? dict 
            : new Dictionary<string, object>();
    }

    public static object? GetContextValue(EvaluationContext context, string path)
    {
        return context.GetByPath(path);
    }

    public static void SetContextValue(EvaluationContext context, string path, object? value)
    {
        context.SetByPath(path, value);
    }
}
