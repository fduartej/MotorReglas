namespace Orchestrator.Features.TemplateRendering.Validation;

public class Validator
{
    private readonly ILogger<Validator> _logger;

    public Validator(ILogger<Validator> logger)
    {
        _logger = logger;
    }

    public ValidationResult ValidateRequiredFields(
        EvaluationContext context, 
        Dictionary<string, TemplateMetadata?> templateMetadata)
    {
        var missingFields = new List<string>();

        foreach (var metadata in templateMetadata)
        {
            if (metadata.Value?.Required != null)
            {
                foreach (var requiredField in metadata.Value.Required)
                {
                    var value = context.GetByPath(requiredField);
                    if (IsNullOrEmpty(value))
                    {
                        missingFields.Add($"{metadata.Key}.{requiredField}");
                        _logger.LogWarning("Required field missing: {TemplateKey}.{Field}", 
                            metadata.Key, requiredField);
                    }
                }
            }
        }

        var isValid = missingFields.Count == 0;
        _logger.LogDebug("Validation result: {IsValid}, Missing fields: {MissingCount}", 
            isValid, missingFields.Count);

        return new ValidationResult(isValid, missingFields);
    }

    public ValidationResult ValidateTemplateRequiredFields(
        EvaluationContext context, 
        string templateKey,
        TemplateMetadata? metadata)
    {
        var missingFields = new List<string>();

        if (metadata?.Required != null)
        {
            foreach (var requiredField in metadata.Required)
            {
                var value = context.GetByPath(requiredField);
                if (IsNullOrEmpty(value))
                {
                    missingFields.Add(requiredField);
                    _logger.LogWarning("Required field missing for template {TemplateKey}: {Field}", 
                        templateKey, requiredField);
                }
            }
        }

        var isValid = missingFields.Count == 0;
        return new ValidationResult(isValid, missingFields);
    }

    public ValidationResult ValidateInputs(Dictionary<string, object> inputs, string[] requiredInputs)
    {
        var missingInputs = new List<string>();

        foreach (var requiredInput in requiredInputs)
        {
            if (!inputs.ContainsKey(requiredInput) || IsNullOrEmpty(inputs[requiredInput]))
            {
                missingInputs.Add(requiredInput);
                _logger.LogWarning("Required input missing: {Input}", requiredInput);
            }
        }

        var isValid = missingInputs.Count == 0;
        _logger.LogDebug("Input validation result: {IsValid}, Missing inputs: {MissingCount}", 
            isValid, missingInputs.Count);

        return new ValidationResult(isValid, missingInputs);
    }

    public ValidationResult ValidateFlowConfig(FlowConfig flow)
    {
        var errors = new List<string>();

        // Basic validation
        ValidateBasicFlowProperties(flow, errors);
        
        // Validate datasets
        ValidateFlowDatasets(flow, errors);

        // Validate references
        ValidateFlowReferences(flow, errors);

        var isValid = errors.Count == 0;
        _logger.LogDebug("Flow validation result: {IsValid}, Errors: {ErrorCount}", isValid, errors.Count);

        return new ValidationResult(isValid, errors);
    }

    private static void ValidateBasicFlowProperties(FlowConfig flow, List<string> errors)
    {
        if (string.IsNullOrEmpty(flow.Name))
            errors.Add("Flow name is required");

        if (flow.Inputs == null || flow.Inputs.Length == 0)
            errors.Add("Flow inputs are required");

        if (flow.Datasets == null || flow.Datasets.Count == 0)
            errors.Add("Flow must have at least one dataset");
    }

    private static void ValidateFlowDatasets(FlowConfig flow, List<string> errors)
    {
        if (flow.Datasets != null)
        {
            for (int i = 0; i < flow.Datasets.Count; i++)
            {
                var dataset = flow.Datasets[i];
                var datasetErrors = ValidateDataset(dataset, i);
                errors.AddRange(datasetErrors);
            }
        }
    }

    private static void ValidateFlowReferences(FlowConfig flow, List<string> errors)
    {
        if (flow.Datasets == null) return;
        
        var datasetNames = flow.Datasets.Select(d => d.Name).ToHashSet();
        
        ValidateMappingReferences(flow.Mapping, datasetNames, errors);
        ValidateCollectionReferences(flow.Collections, datasetNames, errors);
    }

    private static void ValidateMappingReferences(Dictionary<string, string>? mapping, HashSet<string> datasetNames, List<string> errors)
    {
        if (mapping == null) return;
        
        foreach (var map in mapping)
        {
            var sourceParts = map.Value.Split('.');
            if (sourceParts.Length > 0 && !datasetNames.Contains(sourceParts[0]))
            {
                errors.Add($"Mapping references unknown dataset: {sourceParts[0]} in {map.Key}");
            }
        }
    }

    private static void ValidateCollectionReferences(Dictionary<string, CollectionBinding>? collections, HashSet<string> datasetNames, List<string> errors)
    {
        if (collections == null) return;
        
        foreach (var collection in collections)
        {
            if (!datasetNames.Contains(collection.Value.From))
            {
                errors.Add($"Collection '{collection.Key}' references unknown dataset: {collection.Value.From}");
            }
        }
    }

    private static List<string> ValidateDataset(DatasetConfig dataset, int index)
    {
        var errors = new List<string>();

        ValidateBasicDatasetProperties(dataset, index, errors);
        ValidateDatasetByType(dataset, index, errors);
        ValidateDatasetResultMode(dataset, index, errors);

        return errors;
    }

    private static void ValidateBasicDatasetProperties(DatasetConfig dataset, int index, List<string> errors)
    {
        if (string.IsNullOrEmpty(dataset.Name))
            errors.Add($"Dataset[{index}] name is required");

        if (string.IsNullOrEmpty(dataset.Type))
            errors.Add($"Dataset[{index}] type is required");

        if (!IsValidDatasetType(dataset.Type))
            errors.Add($"Dataset[{index}] has invalid type: {dataset.Type}");
    }

    private static void ValidateDatasetByType(DatasetConfig dataset, int index, List<string> errors)
    {
        switch (dataset.Type?.ToLower())
        {
            case "sql":
                ValidateSqlDataset(dataset, index, errors);
                break;
            case "rawsql":
                ValidateRawSqlDataset(dataset, index, errors);
                break;
            case "http":
                ValidateHttpDataset(dataset, index, errors);
                break;
        }
    }

    private static void ValidateSqlDataset(DatasetConfig dataset, int index, List<string> errors)
    {
        if (string.IsNullOrEmpty(dataset.Database))
            errors.Add($"Dataset[{index}] SQL type requires Database");
        if (string.IsNullOrEmpty(dataset.From))
            errors.Add($"Dataset[{index}] SQL type requires From");
    }

    private static void ValidateRawSqlDataset(DatasetConfig dataset, int index, List<string> errors)
    {
        if (string.IsNullOrEmpty(dataset.Database))
            errors.Add($"Dataset[{index}] RawSQL type requires Database");
        if (string.IsNullOrEmpty(dataset.Sql))
            errors.Add($"Dataset[{index}] RawSQL type requires Sql");
    }

    private static void ValidateHttpDataset(DatasetConfig dataset, int index, List<string> errors)
    {
        if (dataset.Http == null)
            errors.Add($"Dataset[{index}] HTTP type requires Http configuration");
        else if (string.IsNullOrEmpty(dataset.Http.Url))
            errors.Add($"Dataset[{index}] HTTP type requires Http.Url");
    }

    private static void ValidateDatasetResultMode(DatasetConfig dataset, int index, List<string> errors)
    {
        if (dataset.Result?.Mode != null && !IsValidResultMode(dataset.Result.Mode))
        {
            errors.Add($"Dataset[{index}] has invalid result mode: {dataset.Result.Mode}");
        }
    }

    private static bool IsValidDatasetType(string? type)
    {
        if (string.IsNullOrEmpty(type)) return false;
        
        return type.ToLower() switch
        {
            "sql" or "rawsql" or "http" => true,
            _ => false
        };
    }

    private static bool IsValidResultMode(string mode)
    {
        return mode.ToLower() switch
        {
            "single" or "array" => true,
            _ => false
        };
    }

    private static bool IsNullOrEmpty(object? value)
    {
        return value switch
        {
            null => true,
            string s => string.IsNullOrWhiteSpace(s),
            ICollection<object> collection => collection.Count == 0,
            _ => false
        };
    }
}

public record ValidationResult(bool IsValid, List<string> Errors)
{
    public bool HasErrors => !IsValid;
    public string ErrorMessage => string.Join("; ", Errors);
}
