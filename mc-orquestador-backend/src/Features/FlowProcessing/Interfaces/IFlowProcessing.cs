using Orchestrator.Features.FlowProcessing.Models;
using Orchestrator;
using System.ComponentModel.DataAnnotations;

namespace Orchestrator.Features.FlowProcessing.Interfaces;

/// <summary>
/// Main interface for flow orchestration
/// </summary>
public interface IFlowOrchestrator
{
    /// <summary>
    /// Executes a flow with the provided input data
    /// </summary>
    /// <param name="flowName">Name of the flow to execute</param>
    /// <param name="inputData">Input data for the flow</param>
    /// <returns>The orchestration result</returns>
    Task<OrchestratorResponse> ExecuteFlowAsync(string flowName, Dictionary<string, object> inputData);

    /// <summary>
    /// Gets the configuration for a specific flow
    /// </summary>
    /// <param name="flowName">Name of the flow</param>
    /// <returns>Flow configuration or null if not found</returns>
    Task<object?> GetFlowConfigAsync(string flowName);

    /// <summary>
    /// Gets a list of available flows
    /// </summary>
    /// <returns>List of available flow names</returns>
    Task<List<string>> GetAvailableFlowsAsync();

    /// <summary>
    /// Clears cache for a specific key
    /// </summary>
    /// <param name="key">Cache key to clear</param>
    Task ClearCacheAsync(string key);
}

/// <summary>
/// Interface for flow configuration loading
/// </summary>
public interface IFlowConfigLoader
{
    /// <summary>
    /// Loads a flow configuration by name
    /// </summary>
    /// <param name="flowName">Name of the flow</param>
    /// <returns>Flow configuration or null if not found</returns>
    Task<FlowConfig?> LoadFlowAsync(string flowName);

    /// <summary>
    /// Gets all available flow names
    /// </summary>
    /// <returns>List of flow names</returns>
    Task<List<string>> GetAvailableFlowsAsync();

    /// <summary>
    /// Validates a flow configuration
    /// </summary>
    /// <param name="flowConfig">Flow configuration to validate</param>
    /// <returns>Validation result</returns>
    ValidationResult ValidateFlow(FlowConfig flowConfig);
}

/// <summary>
/// Interface for context building
/// </summary>
public interface IContextBuilder
{
    /// <summary>
    /// Builds evaluation context from input data
    /// </summary>
    /// <param name="inputData">Input data</param>
    /// <param name="datasetResults">Dataset execution results</param>
    /// <param name="flowConfig">Flow configuration</param>
    /// <returns>Evaluation context</returns>
    EvaluationContext BuildContext(
        Dictionary<string, object> inputData, 
        Dictionary<string, object> datasetResults, 
        FlowConfig flowConfig);

    /// <summary>
    /// Sets a value in the context at the specified path
    /// </summary>
    /// <param name="context">The evaluation context</param>
    /// <param name="path">The path to set</param>
    /// <param name="value">The value to set</param>
    void SetValue(EvaluationContext context, string path, object value);
}

/// <summary>
/// Interface for derived field evaluation
/// </summary>
public interface IDerivedEvaluator
{
    /// <summary>
    /// Evaluates a derived field expression
    /// </summary>
    /// <param name="expression">The expression to evaluate</param>
    /// <param name="context">The evaluation context</param>
    /// <returns>The evaluated value</returns>
    Task<object> EvaluateAsync(string expression, EvaluationContext context);
}
