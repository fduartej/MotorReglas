using System.Text.RegularExpressions;
using Orchestrator;

namespace Orchestrator.Features.FlowProcessing.Services;

public class DerivedEvaluator
{
    private readonly ILogger<DerivedEvaluator> _logger;

    public DerivedEvaluator(ILogger<DerivedEvaluator> logger)
    {
        _logger = logger;
    }

    public Dictionary<string, object> EvaluateDerived(EvaluationContext context, Dictionary<string, DerivedItem>? derivedItems)
    {
        var results = new Dictionary<string, object>();

        if (derivedItems == null || derivedItems.Count == 0)
            return results;

        foreach (var item in derivedItems)
        {
            try
            {
                var value = EvaluateDerivedItem(context, item.Value);
                results[item.Key] = value ?? new object();
                
                _logger.LogTrace("Evaluated derived field {Key}: {Value}", item.Key, value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating derived field {Key}", item.Key);
                results[item.Key] = new object();
            }
        }

        return results;
    }

    private object? EvaluateDerivedItem(EvaluationContext context, DerivedItem item)
    {
        if (item.SumCollectionField != null)
        {
            return EvaluateSumCollectionField(context, item.SumCollectionField);
        }

        if (!string.IsNullOrEmpty(item.Expr))
        {
            return EvaluateExpression(context, item.Expr);
        }

        return null;
    }

    private object? EvaluateSumCollectionField(EvaluationContext context, SumCollectionField sumField)
    {
        var collectionName = sumField.Collection;
        var fieldName = sumField.Field;

        if (!context.TryGetValue(collectionName, out var collectionObj))
        {
            _logger.LogWarning("Collection {CollectionName} not found in context", collectionName);
            return 0;
        }

        if (collectionObj is not IEnumerable<object> collection)
        {
            _logger.LogWarning("Collection {CollectionName} is not enumerable", collectionName);
            return 0;
        }

        decimal sum = 0;
        foreach (var item in collection)
        {
            if (item is Dictionary<string, object> dict && dict.TryGetValue(fieldName, out var fieldValue))
            {
                if (TryConvertToDecimal(fieldValue, out var decimalValue))
                {
                    sum += decimalValue;
                }
            }
        }

        return sum;
    }

    private object? EvaluateExpression(EvaluationContext context, string expression)
    {
        try
        {
            // Simple expression evaluator
            // Supports: > < >= <= == != && || + - * / () and basic field access
            
            var normalizedExpr = NormalizeExpression(expression, context);
            return EvaluateNormalizedExpression(normalizedExpr);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating expression: {Expression}", expression);
            return null;
        }
    }

    private string NormalizeExpression(string expression, EvaluationContext context)
    {
        // Replace field references with actual values
        var result = expression;
        
        // Pattern to match field references (e.g., "field.subfield", "collection[0].field")
        var fieldPattern = @"\b([a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)*(?:\[\d+\])?(?:\.[a-zA-Z_][a-zA-Z0-9_]*)*)\b";
        
        result = Regex.Replace(result, fieldPattern, match =>
        {
            var fieldPath = match.Value;
            
            // Skip if it's a literal (number, boolean, etc.)
            if (IsLiteral(fieldPath)) return fieldPath;
            
            var value = context.GetByPath(fieldPath);
            return FormatValueForExpression(value);
        });

        return result;
    }

    private static bool IsLiteral(string value)
    {
        // Check if it's a number, boolean, or already quoted string
        return double.TryParse(value, out _) || 
               bool.TryParse(value, out _) ||
               value.StartsWith('"') || 
               value.StartsWith('\'') ||
               value is "true" or "false" or "null";
    }

    private static string FormatValueForExpression(object? value)
    {
        return value switch
        {
            null => "null",
            bool b => b.ToString().ToLower(),
            string s => $"\"{s}\"",
            decimal or int or long or double or float => value.ToString() ?? "0",
            _ => $"\"{value}\""
        };
    }

    private object? EvaluateNormalizedExpression(string normalizedExpression)
    {
        // Very simple expression evaluator
        // This is a basic implementation - in production, consider using a proper expression library
        
        try
        {
            // Handle comparison operations
            if (normalizedExpression.Contains("=="))
            {
                var parts = normalizedExpression.Split("==", 2);
                if (parts.Length == 2)
                {
                    var left = EvaluateSimpleValue(parts[0].Trim());
                    var right = EvaluateSimpleValue(parts[1].Trim());
                    return Equals(left, right);
                }
            }

            if (normalizedExpression.Contains("!="))
            {
                var parts = normalizedExpression.Split("!=", 2);
                if (parts.Length == 2)
                {
                    var left = EvaluateSimpleValue(parts[0].Trim());
                    var right = EvaluateSimpleValue(parts[1].Trim());
                    return !Equals(left, right);
                }
            }

            if (normalizedExpression.Contains(">="))
            {
                var parts = normalizedExpression.Split(">=", 2);
                if (parts.Length == 2 && 
                    TryConvertToDecimal(EvaluateSimpleValue(parts[0].Trim()), out var left) &&
                    TryConvertToDecimal(EvaluateSimpleValue(parts[1].Trim()), out var right))
                {
                    return left >= right;
                }
            }

            if (normalizedExpression.Contains("<="))
            {
                var parts = normalizedExpression.Split("<=", 2);
                if (parts.Length == 2 && 
                    TryConvertToDecimal(EvaluateSimpleValue(parts[0].Trim()), out var left) &&
                    TryConvertToDecimal(EvaluateSimpleValue(parts[1].Trim()), out var right))
                {
                    return left <= right;
                }
            }

            if (normalizedExpression.Contains(">"))
            {
                var parts = normalizedExpression.Split(">", 2);
                if (parts.Length == 2 && 
                    TryConvertToDecimal(EvaluateSimpleValue(parts[0].Trim()), out var left) &&
                    TryConvertToDecimal(EvaluateSimpleValue(parts[1].Trim()), out var right))
                {
                    return left > right;
                }
            }

            if (normalizedExpression.Contains("<"))
            {
                var parts = normalizedExpression.Split("<", 2);
                if (parts.Length == 2 && 
                    TryConvertToDecimal(EvaluateSimpleValue(parts[0].Trim()), out var left) &&
                    TryConvertToDecimal(EvaluateSimpleValue(parts[1].Trim()), out var right))
                {
                    return left < right;
                }
            }

            if (normalizedExpression.Contains("&&"))
            {
                var parts = normalizedExpression.Split("&&", 2);
                if (parts.Length == 2)
                {
                    var left = EvaluateNormalizedExpression(parts[0].Trim());
                    var right = EvaluateNormalizedExpression(parts[1].Trim());
                    return IsTrue(left) && IsTrue(right);
                }
            }

            if (normalizedExpression.Contains("||"))
            {
                var parts = normalizedExpression.Split("||", 2);
                if (parts.Length == 2)
                {
                    var left = EvaluateNormalizedExpression(parts[0].Trim());
                    var right = EvaluateNormalizedExpression(parts[1].Trim());
                    return IsTrue(left) || IsTrue(right);
                }
            }

            // Simple arithmetic operations
            if (normalizedExpression.Contains("+"))
            {
                var parts = normalizedExpression.Split("+", 2);
                if (parts.Length == 2 && 
                    TryConvertToDecimal(EvaluateSimpleValue(parts[0].Trim()), out var left) &&
                    TryConvertToDecimal(EvaluateSimpleValue(parts[1].Trim()), out var right))
                {
                    return left + right;
                }
            }

            if (normalizedExpression.Contains("-"))
            {
                var parts = normalizedExpression.Split("-", 2);
                if (parts.Length == 2 && 
                    TryConvertToDecimal(EvaluateSimpleValue(parts[0].Trim()), out var left) &&
                    TryConvertToDecimal(EvaluateSimpleValue(parts[1].Trim()), out var right))
                {
                    return left - right;
                }
            }

            // Single value
            return EvaluateSimpleValue(normalizedExpression.Trim());
        }
        catch
        {
            return null;
        }
    }

    private static object? EvaluateSimpleValue(string value)
    {
        var trimmed = value.Trim();
        
        if (trimmed == "null") return null;
        if (trimmed is "true" or "false") return bool.Parse(trimmed);
        if (trimmed.StartsWith('"') && trimmed.EndsWith('"')) 
            return trimmed[1..^1]; // Remove quotes
        if (decimal.TryParse(trimmed, out var decimalValue)) 
            return decimalValue;
        
        return trimmed;
    }

    private static bool IsTrue(object? value)
    {
        return value switch
        {
            null => false,
            bool b => b,
            decimal d => d != 0,
            int i => i != 0,
            long l => l != 0,
            double d => d != 0,
            float f => f != 0,
            string s => !string.IsNullOrEmpty(s),
            _ => true
        };
    }

    private static bool TryConvertToDecimal(object? value, out decimal result)
    {
        result = 0;
        
        return value switch
        {
            decimal d => (result = d) == d,
            int i => (result = i) == i,
            long l => (result = l) == l,
            double d => (result = (decimal)d) == (decimal)d,
            float f => (result = (decimal)f) == (decimal)f,
            string s => decimal.TryParse(s, out result),
            _ => false
        };
    }
}
