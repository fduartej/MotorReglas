using Scriban;
using Scriban.Runtime;
using System.Text.Json;
using System.Text;
using Orchestrator;
using Polly;
using Orchestrator.Features.DatasetExecution.Sql;

namespace Orchestrator.Features.DatasetExecution.Http;

public class HttpDatasetExecutor
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpDatasetExecutor> _logger;
    private readonly QueryExecutor _queryExecutor; // Para fallback rawSQL

    public HttpDatasetExecutor(HttpClient httpClient, ILogger<HttpDatasetExecutor> logger, QueryExecutor queryExecutor)
    {
        _httpClient = httpClient;
        _logger = logger;
        _queryExecutor = queryExecutor;
    }

    public async Task<object> ExecuteDatasetAsync(DatasetConfig dataset, Dictionary<string, object> inputs)
    {
        if (dataset.Http == null)
            throw new ArgumentException("HTTP dataset requires Http configuration");


        // Siempre usar Polly retry para todas las llamadas HTTP
        var retryConfig = dataset.Http.Retry;
        int maxAttempts = retryConfig?.MaxAttempts ?? 3; // Por defecto 3 intentos
        int backoffMs = retryConfig?.BackoffMs ?? 500;   // Por defecto 500ms

        var retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<Exception>(ex => ex is not null && ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            .WaitAndRetryAsync(
                maxAttempts,
                attempt => TimeSpan.FromMilliseconds(backoffMs * attempt),
                (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception, "[Polly Retry] Intento {RetryCount}/{MaxAttempts} para HTTP dataset {DatasetName}", retryCount, maxAttempts, dataset.Name);
                });

        try
        {
            var response = await retryPolicy.ExecuteAsync(async () =>
            {
                var httpResponse = await ExecuteHttpRequestAsync(dataset.Http, inputs);
                return httpResponse;
            });
            var result = ProcessHttpResponse(response, dataset);
            return dataset.Result.Mode.ToLower() == "single" ?
                (result is IEnumerable<object> enumerable ? enumerable.FirstOrDefault() ?? new object() : result) :
                result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing HTTP dataset {DatasetName}", dataset.Name);
            // Fallback rawSQL si está configurado
            if (inputs != null && dataset.Type == "http" &&
                dataset.OnFailure is not null &&
                dataset.OnFailure.Enabled == true &&
                dataset.OnFailure.FallbackType == "rawSQL" &&
                dataset.OnFailure.FallbackConfig?.Sql != null)
            {
                // Construir un DatasetConfig para rawSQL
                var fallbackDataset = new DatasetConfig(
                    Name: dataset.Name + "_fallback",
                    Type: "rawSQL",
                    Database: dataset.Database,
                    From: null,
                    Select: null,
                    SelectDerived: null,
                    Joins: null,
                    Where: null,
                    OrderBy: null,
                    Limit: null,
                    Result: dataset.Result,
                    Cache: null,
                    Sql: dataset.OnFailure.FallbackConfig.Sql,
                    Params: dataset.Params,
                    Http: null,
                    Extract: null,
                    ResultPaths: null
                );
                _logger.LogWarning("Executing fallback rawSQL for dataset {DatasetName}", dataset.Name);
                return await _queryExecutor.ExecuteDatasetAsync(fallbackDataset, inputs);
            }
            throw;
        }
    }

    private async Task<string> ExecuteHttpRequestAsync(HttpConfig httpConfig, Dictionary<string, object> inputs)
    {
        // Build URL with template substitution
        var url = RenderTemplate(httpConfig.Url, inputs);
        
        // Create request message
        var request = new HttpRequestMessage(new HttpMethod(httpConfig.Method), url);

        // Add headers
        if (httpConfig.Headers != null)
        {
            foreach (var header in httpConfig.Headers)
            {
                var headerValue = RenderTemplate(header.Value, inputs);
                
                if (header.Key.ToLower() == "content-type")
                {
                    request.Content ??= new StringContent("");
                    request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(headerValue);
                }
                else
                {
                    request.Headers.TryAddWithoutValidation(header.Key, headerValue);
                }
            }
        }

        // Add body if specified
        if (!string.IsNullOrEmpty(httpConfig.BodyTemplate))
        {
            var body = RenderTemplate(httpConfig.BodyTemplate, inputs);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        // Manejar timeout por request usando CancellationTokenSource
        _logger.LogDebug("Executing HTTP request: {Method} {Url}", httpConfig.Method, url);

        if (httpConfig.TimeoutMs.HasValue)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(httpConfig.TimeoutMs.Value));
            var response = await _httpClient.SendAsync(request, cts.Token);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        else
        {
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }

    private object ProcessHttpResponse(string responseContent, DatasetConfig dataset)
    {
        if (string.IsNullOrEmpty(responseContent))
            return new object();

        var jsonDocument = JsonDocument.Parse(responseContent);
        var rootElement = jsonDocument.RootElement;

        // Apply result path if specified
        var targetElement = rootElement;
        if (dataset.Http?.ResultPath != null && dataset.Http.ResultPath != "$")
        {
            targetElement = GetElementByPath(rootElement, dataset.Http.ResultPath);
        }

        // Convert to object/array
        var baseResult = JsonElementToObject(targetElement);

        // Apply extract mappings if specified
        if (dataset.Extract != null && dataset.Extract.Count > 0)
        {
            var extractedResult = new Dictionary<string, object>();
            foreach (var extract in dataset.Extract)
            {
                var value = GetValueByJsonPath(rootElement, extract.Value);
                extractedResult[extract.Key] = value ?? new object();
            }
            baseResult = extractedResult;
        }

        // Apply result paths if specified
        if (dataset.ResultPaths != null && dataset.ResultPaths.Count > 0)
        {
            var resultDict = baseResult as Dictionary<string, object> ?? new Dictionary<string, object>();
            
            foreach (var resultPath in dataset.ResultPaths)
            {
                var value = GetValueByJsonPath(rootElement, resultPath.Path);
                resultDict[resultPath.As] = value ?? new object();
            }
            
            return resultDict;
        }

        return baseResult;
    }

    private static JsonElement GetElementByPath(JsonElement element, string path)
    {
        if (string.IsNullOrEmpty(path) || path == "$")
            return element;

        var parts = path.Split('.');
        var current = element;

        foreach (var part in parts)
        {
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var property))
            {
                current = property;
            }
            else if (current.ValueKind == JsonValueKind.Array && int.TryParse(part, out var index))
            {
                if (index >= 0 && index < current.GetArrayLength())
                {
                    current = current[index];
                }
                else
                {
                    return default;
                }
            }
            else
            {
                return default;
            }
        }

        return current;
    }

    private static object? GetValueByJsonPath(JsonElement rootElement, string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        // Handle special cases
        if (path == "length" && rootElement.ValueKind == JsonValueKind.Array)
        {
            return rootElement.GetArrayLength();
        }

        var parts = path.Split('.');
        var current = rootElement;

        foreach (var part in parts)
        {
            if (part == "length" && current.ValueKind == JsonValueKind.Array)
            {
                return current.GetArrayLength();
            }

            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var property))
            {
                current = property;
            }
            else if (current.ValueKind == JsonValueKind.Array && int.TryParse(part, out var index))
            {
                if (index >= 0 && index < current.GetArrayLength())
                {
                    current = current[index];
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

        return JsonElementToObject(current);
    }

    private static object JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(prop => prop.Name, prop => JsonElementToObject(prop.Value)),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(JsonElementToObject).ToList(),
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.TryGetInt64(out var longValue) ? longValue : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => new object(),
            _ => new object()
        };
    }

    private static string RenderTemplate(string template, Dictionary<string, object> inputs)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        try
        {
            // Handle environment variables
            var processedTemplate = template;
            var envPattern = @"\{\{ENV\.(\w+)\}\}";
            processedTemplate = System.Text.RegularExpressions.Regex.Replace(processedTemplate, envPattern, match =>
            {
                var envVar = match.Groups[1].Value;
                return Environment.GetEnvironmentVariable(envVar) ?? "";
            });

            // Use Scriban for template rendering
            var scribanTemplate = Template.Parse(processedTemplate);
            var context = new TemplateContext();
            
            // Add inputs to context
            var scriptObject = new Scriban.Runtime.ScriptObject();
            foreach (var input in inputs)
            {
                scriptObject[input.Key] = input.Value;
            }
            context.PushGlobal(scriptObject);

            return scribanTemplate.Render(context);
        }
        catch (Exception)
        {
            // Fallback to simple string replacement
            var result = template;
            foreach (var input in inputs)
            {
                result = result.Replace($"{{{{{input.Key}}}}}", input.Value?.ToString() ?? "");
            }
            return result;
        }
    }
}
