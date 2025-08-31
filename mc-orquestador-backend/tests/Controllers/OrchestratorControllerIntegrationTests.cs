using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Orchestrator.Controllers;
using Orchestrator.Features.FlowProcessing.Interfaces;
using Orchestrator.Features.FlowProcessing.Models;
using Xunit;

namespace Orchestrator.Tests.Controllers;

/// <summary>
/// Integration tests for OrchestratorController focusing on edge cases and validation
/// </summary>
public class OrchestratorControllerIntegrationTests
{
    private readonly Mock<IFlowOrchestrator> _mockFlowOrchestrator;
    private readonly Mock<ILogger<OrchestratorController>> _mockLogger;
    private readonly OrchestratorController _controller;

    public OrchestratorControllerIntegrationTests()
    {
        _mockFlowOrchestrator = new Mock<IFlowOrchestrator>();
        _mockLogger = new Mock<ILogger<OrchestratorController>>();
        _controller = new OrchestratorController(_mockFlowOrchestrator.Object, _mockLogger.Object);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task BuildPayload_WithInvalidFlowNames_HandlesGracefully(string? invalidFlowName)
    {
        // Arrange
        var request = new PayloadRequest
        {
            InputData = new Dictionary<string, object>
            {
                ["dni"] = "12345678",
                ["transaccionId"] = 987654321
            }
        };

        _mockFlowOrchestrator
            .Setup(x => x.ExecuteFlowAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
            .ThrowsAsync(new ArgumentException($"Flow name '{invalidFlowName}' is invalid"));

        // Act
        var result = await _controller.BuildPayload(invalidFlowName!, request);

        // Assert
        result.Should().NotBeNull();
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().NotBeNull();
        
        var json = System.Text.Json.JsonSerializer.Serialize(notFoundResult.Value);
        json.Should().Contain("error");
    }

    [Fact]
    public async Task BuildPayload_WithNullInputData_HandlesCorrectly()
    {
        // Arrange
        var flowName = "FNB";
        var request = new PayloadRequest
        {
            InputData = null!
        };

        _mockFlowOrchestrator
            .Setup(x => x.ExecuteFlowAsync(flowName, It.IsAny<Dictionary<string, object>>()))
            .ThrowsAsync(new ArgumentNullException("inputData", "Input data cannot be null"));

        // Act
        var result = await _controller.BuildPayload(flowName, request);

        // Assert
        result.Should().NotBeNull();
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetFlowConfig_WithSpecialCharactersInFlowName_HandlesCorrectly()
    {
        // Arrange
        var flowName = "FNB@#$%^&*()";

        _mockFlowOrchestrator
            .Setup(x => x.GetFlowConfigAsync(flowName))
            .ReturnsAsync((object?)null);

        // Act
        var result = await _controller.GetFlowConfig(flowName);

        // Assert
        result.Should().NotBeNull();
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().NotBeNull();
        
        var json = System.Text.Json.JsonSerializer.Serialize(notFoundResult.Value);
        json.Should().Contain("error");
        json.Should().Contain("FNB"); // Just check for the basic part without special characters
    }

    [Fact]
    public async Task ClearCache_WithVeryLongCacheKey_HandlesCorrectly()
    {
        // Arrange
        var longCacheKey = new string('A', 1000); // Very long cache key
        
        _mockFlowOrchestrator
            .Setup(x => x.ClearCacheAsync(longCacheKey))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ClearCache(longCacheKey);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
        
        _mockFlowOrchestrator.Verify(x => x.ClearCacheAsync(longCacheKey), Times.Once);
    }

    [Fact]
    public async Task GetFlows_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        _mockFlowOrchestrator
            .Setup(x => x.GetAvailableFlowsAsync())
            .ThrowsAsync(new InvalidOperationException("Service temporarily unavailable"));

        // Act
        var result = await _controller.GetFlows();

        // Assert
        result.Should().NotBeNull();
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(500);
        
        var json = System.Text.Json.JsonSerializer.Serialize(statusCodeResult.Value);
        json.Should().Contain("error");
    }

    [Fact]
    public async Task GetFlowConfig_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var flowName = "FNB";
        
        _mockFlowOrchestrator
            .Setup(x => x.GetFlowConfigAsync(flowName))
            .ThrowsAsync(new InvalidOperationException("Configuration service down"));

        // Act
        var result = await _controller.GetFlowConfig(flowName);

        // Assert
        result.Should().NotBeNull();
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(500);
        
        var json = System.Text.Json.JsonSerializer.Serialize(statusCodeResult.Value);
        json.Should().Contain("error");
    }

    [Fact]
    public async Task BuildPayload_WithLargeInputData_ProcessesCorrectly()
    {
        // Arrange
        var flowName = "FNB";
        var largeInputData = new Dictionary<string, object>();
        
        // Create large input data
        for (int i = 0; i < 100; i++)
        {
            largeInputData[$"field_{i}"] = $"value_{i}";
            largeInputData[$"nested_{i}"] = new Dictionary<string, object>
            {
                ["subfield"] = $"subvalue_{i}",
                ["array"] = new[] { 1, 2, 3, 4, 5 }
            };
        }

        var request = new PayloadRequest
        {
            InputData = largeInputData
        };

        var expectedResponse = new OrchestratorResponse
        {
            IsSuccess = true,
            Template = "FNB_Large",
            Payload = new { processed = true, count = largeInputData.Count },
            Debug = new DebugInfo
            {
                TraceId = "large-data-trace",
                ElapsedMs = 500,
                Datasets = new Dictionary<string, object>(),
                Derived = new Dictionary<string, object>(),
                InputData = largeInputData,
                Context = new Dictionary<string, object>()
            }
        };

        _mockFlowOrchestrator
            .Setup(x => x.ExecuteFlowAsync(flowName, largeInputData))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.BuildPayload(flowName, request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<OrchestratorResponse>().Subject;
        
        response.IsSuccess.Should().BeTrue();
        response.Template.Should().Be("FNB_Large");
        response.Debug.Should().NotBeNull();
        response.Debug!.InputData.Should().HaveCount(200); // 100 fields + 100 nested fields
        
        _mockFlowOrchestrator.Verify(x => x.ExecuteFlowAsync(flowName, largeInputData), Times.Once);
    }

    [Fact]
    public void Health_AlwaysReturnsHealthyStatus()
    {
        // Act - Call multiple times to ensure consistency
        var result1 = _controller.Health();
        var result2 = _controller.Health();
        var result3 = _controller.Health();

        // Assert
        foreach (var result in new[] { result1, result2, result3 })
        {
            result.Should().NotBeNull();
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().NotBeNull();
            
            var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            json.Should().Contain("healthy");
            json.Should().Contain("orchestrator");
        }
    }

    [Fact]
    public void Metrics_ReturnsConsistentStructure()
    {
        // Act - Call multiple times to ensure structure consistency
        var result1 = _controller.Metrics();
        var result2 = _controller.Metrics();

        // Assert
        foreach (var result in new[] { result1, result2 })
        {
            result.Should().NotBeNull();
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().NotBeNull();
            
            var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            json.Should().Contain("timestamp");
            json.Should().Contain("uptime");
            json.Should().Contain("environment");
        }
    }
}
