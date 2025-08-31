using Microsoft.AspNetCore.Mvc;
using Orchestrator.Features.FlowProcessing.Interfaces;
using Orchestrator.Features.FlowProcessing.Models;

namespace Orchestrator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrchestratorController : ControllerBase
{
    private readonly IFlowOrchestrator _flowOrchestrator;
    private readonly ILogger<OrchestratorController> _logger;

    public OrchestratorController(
        IFlowOrchestrator flowOrchestrator,
        ILogger<OrchestratorController> logger)
    {
        _flowOrchestrator = flowOrchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Builds a payload based on the specified flow and input data
    /// </summary>
    /// <param name="flow">The flow name to execute</param>
    /// <param name="request">The input data for the flow</param>
    /// <returns>The orchestrated result with payload and debug information</returns>
    [HttpPost("build-payload/{flow}")]
    public async Task<ActionResult<OrchestratorResponse>> BuildPayload(
        string flow, 
        [FromBody] PayloadRequest request)
    {
        try
        {
            _logger.LogInformation("Building payload for flow: {FlowName}", flow);
            
            var result = await _flowOrchestrator.ExecuteFlowAsync(flow, request.InputData);
            
            if (result.IsSuccess)
            {
                return Ok(result);
            }
            
            return BadRequest(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request for flow: {FlowName}", flow);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing flow: {FlowName}", flow);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public ActionResult<object> Health()
    {
        return Ok(new { 
            status = "healthy", 
            timestamp = DateTime.UtcNow,
            service = "orchestrator"
        });
    }

    /// <summary>
    /// Get flow configuration
    /// </summary>
    [HttpGet("flows/{flow}/config")]
    public async Task<ActionResult> GetFlowConfig(string flow)
    {
        try
        {
            var config = await _flowOrchestrator.GetFlowConfigAsync(flow);
            if (config == null)
            {
                return NotFound(new { error = $"Flow '{flow}' not found" });
            }
            
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting flow config: {FlowName}", flow);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get available flows
    /// </summary>
    [HttpGet("flows")]
    public async Task<ActionResult> GetFlows()
    {
        try
        {
            var flows = await _flowOrchestrator.GetAvailableFlowsAsync();
            return Ok(new { flows });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available flows");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Clear cache for a specific key
    /// </summary>
    [HttpDelete("cache/{key}")]
    public async Task<ActionResult> ClearCache(string key)
    {
        try
        {
            await _flowOrchestrator.ClearCacheAsync(key);
            return Ok(new { message = $"Cache key '{key}' cleared" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache for key: {CacheKey}", key);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get metrics and system information
    /// </summary>
    [HttpGet("metrics")]
    public ActionResult<object> Metrics()
    {
        return Ok(new
        {
            timestamp = DateTime.UtcNow,
            uptime = Environment.TickCount64,
            version = typeof(Program).Assembly.GetName().Version?.ToString(),
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
        });
    }
}
