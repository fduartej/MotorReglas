using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Scriban;
using Scriban.Runtime;
using System.Text.Json;
using Orchestrator;

namespace Orchestrator.Features.TemplateRendering.Rendering;

public class TemplateRenderer
{
    private readonly IFileProvider _fileProvider;
    private readonly IMemoryCache _cache;
    private readonly ExternalConfig _config;
    private readonly ILogger<TemplateRenderer> _logger;

    public TemplateRenderer(
        [FromKeyedServices("templates")] IFileProvider fileProvider,
        IMemoryCache cache,
        ExternalConfig config,
        ILogger<TemplateRenderer> logger)
    {
        _fileProvider = fileProvider;
        _cache = cache;
        _config = config;
        _logger = logger;
    }

    public async Task<object> RenderTemplateAsync(string templatePath, EvaluationContext context)
    {
        try
        {
            var templateInfo = await LoadTemplateAsync(templatePath);
            if (templateInfo == null)
            {
                throw new FileNotFoundException($"Template not found: {templatePath}");
            }

            var renderedJson = RenderTemplate(templateInfo.Content, context);
            var result = JsonSerializer.Deserialize<object>(renderedJson);
            
            _logger.LogDebug("Rendered template {TemplatePath} successfully", templatePath);
            return result ?? new object();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering template {TemplatePath}", templatePath);
            throw;
        }
    }

    public async Task<Dictionary<string, object>> RenderMultipleTemplatesAsync(
        Dictionary<string, string> templatePaths, 
        EvaluationContext context)
    {
        var results = new Dictionary<string, object>();

        foreach (var templatePath in templatePaths)
        {
            try
            {
                var result = await RenderTemplateAsync(templatePath.Value, context);
                results[templatePath.Key] = result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rendering template {TemplateKey}: {TemplatePath}", 
                    templatePath.Key, templatePath.Value);
                results[templatePath.Key] = new object();
            }
        }

        return results;
    }

    private async Task<TemplateInfo?> LoadTemplateAsync(string templatePath)
    {
        var cacheKey = $"template:{templatePath}";
        
        if (_cache.TryGetValue(cacheKey, out TemplateInfo? cachedTemplate))
        {
            _logger.LogTrace("Template {TemplatePath} loaded from cache", templatePath);
            return cachedTemplate;
        }

        var fileInfo = _fileProvider.GetFileInfo(templatePath);
        if (!fileInfo.Exists)
        {
            _logger.LogWarning("Template file {TemplatePath} not found", templatePath);
            return null;
        }

        try
        {
            var content = await ReadFileContentAsync(fileInfo);
            var templateInfo = new TemplateInfo(templatePath, content);

            // Cache with file change token
            var changeToken = _fileProvider.Watch(templatePath);
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(_config.CacheSeconds))
                .AddExpirationToken(changeToken)
                .SetPriority(CacheItemPriority.High);

            _cache.Set(cacheKey, templateInfo, cacheOptions);
            
            _logger.LogInformation("Template {TemplatePath} loaded and cached", templatePath);
            return templateInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading template {TemplatePath}", templatePath);
            return null;
        }
    }

    private string RenderTemplate(string templateContent, EvaluationContext context)
    {
        try
        {
            var template = Template.Parse(templateContent);
            if (template.HasErrors)
            {
                var errors = string.Join("; ", template.Messages.Select(m => m.Message));
                throw new InvalidOperationException($"Template parsing errors: {errors}");
            }

            var scribanContext = CreateScribanContext(context);
            var result = template.Render(scribanContext);
            
            _logger.LogTrace("Template rendered successfully. Length: {Length}", result.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering template content");
            throw;
        }
    }

    private static TemplateContext CreateScribanContext(EvaluationContext context)
    {
        var scribanContext = new TemplateContext();
        var scriptObject = new Scriban.Runtime.ScriptObject();
        
        foreach (var item in context)
        {
            scriptObject[item.Key] = item.Value;
        }
        
        scribanContext.PushGlobal(scriptObject);
        return scribanContext;
    }

    private static async Task<string> ReadFileContentAsync(IFileInfo fileInfo)
    {
        using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    public async Task<TemplateMetadata?> GetTemplateMetadataAsync(string templatePath)
    {
        try
        {
            var templateInfo = await LoadTemplateAsync(templatePath);
            if (templateInfo == null)
                return null;

            // Parse the template to extract _meta section
            var jsonDocument = JsonDocument.Parse(templateInfo.Content);
            if (jsonDocument.RootElement.TryGetProperty("_meta", out var metaElement) &&
                metaElement.TryGetProperty("required", out var requiredElement) && 
                requiredElement.ValueKind == JsonValueKind.Array)
            {
                var required = requiredElement.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString()!)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();

                return new TemplateMetadata(required);
            }

            return new TemplateMetadata(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting template metadata for {TemplatePath}", templatePath);
            return null;
        }
    }

    public async Task<Dictionary<string, TemplateMetadata?>> GetMultipleTemplateMetadataAsync(
        Dictionary<string, string> templatePaths)
    {
        var results = new Dictionary<string, TemplateMetadata?>();

        foreach (var templatePath in templatePaths)
        {
            var metadata = await GetTemplateMetadataAsync(templatePath.Value);
            results[templatePath.Key] = metadata;
        }

        return results;
    }

    public void ClearTemplateCache(string? templatePath = null)
    {
        if (templatePath != null)
        {
            var cacheKey = $"template:{templatePath}";
            _cache.Remove(cacheKey);
            _logger.LogDebug("Cleared template cache for {TemplatePath}", templatePath);
        }
        else
        {
            // Note: IMemoryCache doesn't have a clear all method
            // This would require a custom cache implementation to track all keys
            _logger.LogDebug("Template cache clear requested (individual clearing only)");
        }
    }

    public bool IsTemplateInCache(string templatePath)
    {
        var cacheKey = $"template:{templatePath}";
        return _cache.TryGetValue(cacheKey, out _);
    }
}
