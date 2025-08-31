namespace Orchestrator.Features.TemplateRendering.Selection;

using Orchestrator;

public class TemplateSelector
{
    private readonly ILogger<TemplateSelector> _logger;

    public TemplateSelector(ILogger<TemplateSelector> logger)
    {
        _logger = logger;
    }

    public Dictionary<string, string> SelectTemplates(EvaluationContext context, TemplateSection templateSection)
    {
        var selectedTemplates = new Dictionary<string, string>();

        try
        {
            // Select motor decision template
            var motorTemplate = SelectTemplate(context, templateSection.MotorDecision);
            selectedTemplates["motorDecision"] = motorTemplate;

            // Select SAP template
            var sapTemplate = SelectTemplate(context, templateSection.Sap);
            selectedTemplates["sap"] = sapTemplate;

            _logger.LogInformation("Selected templates - Motor: {MotorTemplate}, SAP: {SapTemplate}", 
                motorTemplate, sapTemplate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting templates");
            throw;
        }

        return selectedTemplates;
    }

    private string SelectTemplate(EvaluationContext context, TemplateGroup templateGroup)
    {
        if (templateGroup.Rules != null && templateGroup.Rules.Count > 0)
        {
            foreach (var rule in templateGroup.Rules)
            {
                if (EvaluateRule(context, rule))
                {
                    _logger.LogDebug("Template rule matched, selecting: {Template}", rule.Template);
                    return rule.Template;
                }
            }
        }

        _logger.LogDebug("No template rules matched, using default: {DefaultTemplate}", templateGroup.Default);
        return templateGroup.Default;
    }

    private bool EvaluateRule(EvaluationContext context, TemplateRule rule)
    {
        if (rule.If == null || rule.If.Count == 0)
            return false;

        // All conditions must be true (AND logic)
        foreach (var condition in rule.If)
        {
            if (!EvaluateCondition(context, condition))
            {
                _logger.LogTrace("Template condition failed: {Path} {Op} {Value}", 
                    condition.Path, condition.Op, condition.Value);
                return false;
            }
        }

        _logger.LogTrace("All template conditions passed for template: {Template}", rule.Template);
        return true;
    }

    private bool EvaluateCondition(EvaluationContext context, TemplateCond condition)
    {
        try
        {
            var contextValue = context.GetByPath(condition.Path);
            var conditionValue = condition.Value;

            return condition.Op.ToLower() switch
            {
                "=" or "==" => AreEqual(contextValue, conditionValue),
                "!=" or "<>" => !AreEqual(contextValue, conditionValue),
                ">" => IsGreaterThan(contextValue, conditionValue),
                ">=" => IsGreaterThanOrEqual(contextValue, conditionValue),
                "<" => IsLessThan(contextValue, conditionValue),
                "<=" => IsLessThanOrEqual(contextValue, conditionValue),
                "in" => IsInCollection(contextValue, conditionValue),
                "contains" => Contains(contextValue, conditionValue),
                "startswith" => StartsWith(contextValue, conditionValue),
                "endswith" => EndsWith(contextValue, conditionValue),
                _ => false
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error evaluating condition: {Path} {Op} {Value}", 
                condition.Path, condition.Op, condition.Value);
            return false;
        }
    }

    private static bool AreEqual(object? left, object? right)
    {
        if (left == null && right == null) return true;
        if (left == null || right == null) return false;

        // Try numeric comparison first
        if (TryConvertToDecimal(left, out var leftDecimal) && 
            TryConvertToDecimal(right, out var rightDecimal))
        {
            return leftDecimal == rightDecimal;
        }

        // Try boolean comparison
        if (left is bool leftBool && right is bool rightBool)
        {
            return leftBool == rightBool;
        }

        // String comparison (case-insensitive)
        var leftStr = left.ToString();
        var rightStr = right.ToString();
        return string.Equals(leftStr, rightStr, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGreaterThan(object? left, object? right)
    {
        if (TryConvertToDecimal(left, out var leftDecimal) && 
            TryConvertToDecimal(right, out var rightDecimal))
        {
            return leftDecimal > rightDecimal;
        }

        return false;
    }

    private static bool IsGreaterThanOrEqual(object? left, object? right)
    {
        if (TryConvertToDecimal(left, out var leftDecimal) && 
            TryConvertToDecimal(right, out var rightDecimal))
        {
            return leftDecimal >= rightDecimal;
        }

        return false;
    }

    private static bool IsLessThan(object? left, object? right)
    {
        if (TryConvertToDecimal(left, out var leftDecimal) && 
            TryConvertToDecimal(right, out var rightDecimal))
        {
            return leftDecimal < rightDecimal;
        }

        return false;
    }

    private static bool IsLessThanOrEqual(object? left, object? right)
    {
        if (TryConvertToDecimal(left, out var leftDecimal) && 
            TryConvertToDecimal(right, out var rightDecimal))
        {
            return leftDecimal <= rightDecimal;
        }

        return false;
    }

    private static bool IsInCollection(object? value, object? collection)
    {
        if (collection is IEnumerable<object> enumerable)
        {
            return enumerable.Any(item => AreEqual(value, item));
        }

        return false;
    }

    private static bool Contains(object? haystack, object? needle)
    {
        if (haystack == null || needle == null) return false;

        var haystackStr = haystack.ToString();
        var needleStr = needle.ToString();
        
        return haystackStr != null && needleStr != null && 
               haystackStr.Contains(needleStr, StringComparison.OrdinalIgnoreCase);
    }

    private static bool StartsWith(object? text, object? prefix)
    {
        if (text == null || prefix == null) return false;

        var textStr = text.ToString();
        var prefixStr = prefix.ToString();
        
        return textStr != null && prefixStr != null && 
               textStr.StartsWith(prefixStr, StringComparison.OrdinalIgnoreCase);
    }

    private static bool EndsWith(object? text, object? suffix)
    {
        if (text == null || suffix == null) return false;

        var textStr = text.ToString();
        var suffixStr = suffix.ToString();
        
        return textStr != null && suffixStr != null && 
               textStr.EndsWith(suffixStr, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryConvertToDecimal(object? value, out decimal result)
    {
        result = 0;
        
        switch (value)
        {
            case decimal d:
                result = d;
                return true;
            case int i:
                result = i;
                return true;
            case long l:
                result = l;
                return true;
            case double d:
                result = (decimal)d;
                return true;
            case float f:
                result = (decimal)f;
                return true;
            case string s:
                return decimal.TryParse(s, out result);
            default:
                return false;
        }
    }
}
