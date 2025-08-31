using System.ComponentModel.DataAnnotations;

namespace Orchestrator.Features.FlowProcessing.Models;

/// <summary>
/// Request model for payload building
/// </summary>
public class PayloadRequest
{
    /// <summary>
    /// Input data for the flow execution
    /// </summary>
    [Required]
    public Dictionary<string, object> InputData { get; set; } = new();
}

/// <summary>
/// Response model for orchestrator operations
/// </summary>
public class OrchestratorResponse
{
    /// <summary>
    /// Indicates if the operation was successful
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// The selected template name
    /// </summary>
    public string? Template { get; set; }

    /// <summary>
    /// The rendered payload object
    /// </summary>
    public object? Payload { get; set; }

    /// <summary>
    /// Error message if operation failed
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Debug information
    /// </summary>
    public DebugInfo? Debug { get; set; }

    /// <summary>
    /// Missing required fields if validation failed
    /// </summary>
    public List<string>? MissingFields { get; set; }
}

/// <summary>
/// Debug information for troubleshooting
/// </summary>
public class DebugInfo
{
    /// <summary>
    /// Trace identifier
    /// </summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// Execution time in milliseconds
    /// </summary>
    public long ElapsedMs { get; set; }

    /// <summary>
    /// Dataset execution results
    /// </summary>
    public Dictionary<string, object> Datasets { get; set; } = new();

    /// <summary>
    /// Derived field evaluations
    /// </summary>
    public Dictionary<string, object> Derived { get; set; } = new();

    /// <summary>
    /// Input data provided
    /// </summary>
    public Dictionary<string, object> InputData { get; set; } = new();

    /// <summary>
    /// Final evaluation context
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();

    /// <summary>
    /// Post-processing execution results
    /// </summary>
    public Dictionary<string, object> PostProcessingResults { get; set; } = new();
}

/// <summary>
/// Flow execution result
/// </summary>
public class FlowExecutionResult
{
    /// <summary>
    /// Indicates if execution was successful
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// The orchestrator response
    /// </summary>
    public OrchestratorResponse Response { get; set; } = new();

    /// <summary>
    /// Exception that occurred during execution
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static FlowExecutionResult Success(OrchestratorResponse response)
    {
        return new FlowExecutionResult
        {
            IsSuccess = true,
            Response = response
        };
    }

    /// <summary>
    /// Creates a failed result
    /// </summary>
    public static FlowExecutionResult Failure(string error, Exception? exception = null)
    {
        return new FlowExecutionResult
        {
            IsSuccess = false,
            Response = new OrchestratorResponse
            {
                IsSuccess = false,
                Error = error
            },
            Exception = exception
        };
    }
}
