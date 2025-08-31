using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Orchestrator.Controllers;
using Orchestrator.Features.FlowProcessing.Interfaces;
using Orchestrator.Features.FlowProcessing.Models;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace Orchestrator.Tests.Controllers;

public class OrchestratorControllerTests
{
    private readonly Mock<IFlowOrchestrator> _mockFlowOrchestrator;
    private readonly Mock<ILogger<OrchestratorController>> _mockLogger;
    private readonly OrchestratorController _controller;

    public OrchestratorControllerTests()
    {
        _mockFlowOrchestrator = new Mock<IFlowOrchestrator>();
        _mockLogger = new Mock<ILogger<OrchestratorController>>();
        _controller = new OrchestratorController(_mockFlowOrchestrator.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task BuildPayload_WithValidRequest_ReturnsOkResult()
    {
        // Arrange
        var flowName = "FNB";
        var request = new PayloadRequest
        {
            InputData = new Dictionary<string, object>
            {
                ["dni"] = "12345678",
                ["transaccionId"] = 987654321,
                ["canal"] = "web"
            }
        };

        var expectedResponse = new OrchestratorResponse
        {
            IsSuccess = true,
            Template = "FNB_Base",
            Payload = new { message = "success" },
            Debug = new DebugInfo
            {
                TraceId = "test-trace-id",
                ElapsedMs = 100,
                Datasets = new Dictionary<string, object>(),
                Derived = new Dictionary<string, object>(),
                InputData = request.InputData,
                Context = new Dictionary<string, object>()
            }
        };

        _mockFlowOrchestrator
            .Setup(x => x.ExecuteFlowAsync(flowName, request.InputData))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.BuildPayload(flowName, request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<OrchestratorResponse>().Subject;
        
        response.IsSuccess.Should().BeTrue();
        response.Template.Should().Be("FNB_Base");
        response.Payload.Should().NotBeNull();
        response.Debug.Should().NotBeNull();
        response.Debug!.TraceId.Should().Be("test-trace-id");
        response.Debug.ElapsedMs.Should().Be(100);
    }

    [Fact]
    public async Task BuildPayload_WithFailedExecution_ReturnsBadRequest()
    {
        // Arrange
        var flowName = "FNB";
        var request = new PayloadRequest
        {
            InputData = new Dictionary<string, object>
            {
                ["dni"] = "12345678"
            }
        };

        var expectedResponse = new OrchestratorResponse
        {
            IsSuccess = false,
            Error = "Flow execution failed",
            Debug = new DebugInfo
            {
                TraceId = "test-trace-id",
                ElapsedMs = 50,
                Datasets = new Dictionary<string, object>(),
                Derived = new Dictionary<string, object>(),
                InputData = request.InputData,
                Context = new Dictionary<string, object>()
            }
        };

        _mockFlowOrchestrator
            .Setup(x => x.ExecuteFlowAsync(flowName, request.InputData))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.BuildPayload(flowName, request);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<OrchestratorResponse>().Subject;
        
        response.IsSuccess.Should().BeFalse();
        response.Error.Should().Be("Flow execution failed");
        response.Debug.Should().NotBeNull();
    }

    [Fact]
    public async Task BuildPayload_WithException_ReturnsInternalServerError()
    {
        // Arrange
        var flowName = "FNB";
        var request = new PayloadRequest
        {
            InputData = new Dictionary<string, object>
            {
                ["dni"] = "12345678"
            }
        };

        _mockFlowOrchestrator
            .Setup(x => x.ExecuteFlowAsync(flowName, request.InputData))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _controller.BuildPayload(flowName, request);

        // Assert
        result.Should().NotBeNull();
        var statusCodeResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task GetFlowConfig_WithValidFlowName_ReturnsOkResult()
    {
        // Arrange
        var flowName = "FNB";
        var expectedConfig = new
        {
            name = "FNB",
            description = "Flow para procesamiento FNB",
            version = "1.0"
        };

        _mockFlowOrchestrator
            .Setup(x => x.GetFlowConfigAsync(flowName))
            .ReturnsAsync(expectedConfig);

        // Act
        var result = await _controller.GetFlowConfig(flowName);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expectedConfig);
    }

    [Fact]
    public async Task GetFlowConfig_WithInvalidFlowName_ReturnsNotFound()
    {
        // Arrange
        var flowName = "NonExistentFlow";

        _mockFlowOrchestrator
            .Setup(x => x.GetFlowConfigAsync(flowName))
            .ReturnsAsync((object?)null);

        // Act
        var result = await _controller.GetFlowConfig(flowName);

        // Assert
        result.Should().NotBeNull();
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().NotBeNull();
        
        // Verify the structure contains error message
        var json = System.Text.Json.JsonSerializer.Serialize(notFoundResult.Value);
        json.Should().Contain("error");
        json.Should().Contain(flowName);
    }

    [Fact]
    public async Task GetFlows_ReturnsOkResultWithFlowList()
    {
        // Arrange
        var expectedFlows = new List<string> { "FNB", "CREDITO", "INVERSIONES" };

        _mockFlowOrchestrator
            .Setup(x => x.GetAvailableFlowsAsync())
            .ReturnsAsync(expectedFlows);

        // Act
        var result = await _controller.GetFlows();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
        
        // Verify the structure contains flows
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        json.Should().Contain("flows");
        json.Should().Contain("FNB");
        json.Should().Contain("CREDITO");
        json.Should().Contain("INVERSIONES");
    }

    [Fact]
    public async Task GetFlows_WithEmptyFlowList_ReturnsOkResultWithEmptyList()
    {
        // Arrange
        var expectedFlows = new List<string>();

        _mockFlowOrchestrator
            .Setup(x => x.GetAvailableFlowsAsync())
            .ReturnsAsync(expectedFlows);

        // Act
        var result = await _controller.GetFlows();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task ClearCache_ReturnsOkResult()
    {
        // Arrange
        var cacheKey = "flow:FNB";
        _mockFlowOrchestrator
            .Setup(x => x.ClearCacheAsync(cacheKey))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ClearCache(cacheKey);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
        
        _mockFlowOrchestrator.Verify(x => x.ClearCacheAsync(cacheKey), Times.Once);
    }

    [Fact]
    public async Task ClearCache_WithException_ReturnsInternalServerError()
    {
        // Arrange
        var cacheKey = "flow:FNB";
        _mockFlowOrchestrator
            .Setup(x => x.ClearCacheAsync(cacheKey))
            .ThrowsAsync(new Exception("Cache service unavailable"));

        // Act
        var result = await _controller.ClearCache(cacheKey);

        // Assert
        result.Should().NotBeNull();
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(500);
    }

    [Fact]
    public void Health_ReturnsOkResultWithHealthInfo()
    {
        // Act
        var result = _controller.Health();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
        
        // Verify the structure contains expected properties
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        json.Should().Contain("status");
        json.Should().Contain("timestamp");
        json.Should().Contain("service");
    }

    [Fact]
    public void Metrics_ReturnsOkResultWithMetricsInfo()
    {
        // Act
        var result = _controller.Metrics();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
        
        // Verify the structure contains expected properties
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        json.Should().Contain("timestamp");
        json.Should().Contain("uptime");
    }

    [Fact]
    public async Task BuildPayload_WithComplexInputData_ProcessesCorrectly()
    {
        // Arrange
        var flowName = "FNB";
        var request = new PayloadRequest
        {
            InputData = new Dictionary<string, object>
            {
                ["dni"] = "12345678",
                ["transaccionId"] = 987654321,
                ["canal"] = "mobile",
                ["pais"] = "CO",
                ["tipoCliente"] = "premium",
                ["cuentaContrato"] = "PREM789",
                ["metadata"] = new Dictionary<string, object>
                {
                    ["ip"] = "192.168.1.100",
                    ["userAgent"] = "Mobile App v2.1",
                    ["sessionId"] = "sess_12345"
                }
            }
        };

        var expectedResponse = new OrchestratorResponse
        {
            IsSuccess = true,
            Template = "FNB_Premium",
            Payload = new
            {
                transaccionId = 987654321,
                cliente = new { dni = "12345678", tipo = "premium" },
                configuracion = new { template = "FNB_Premium", esPremium = true }
            },
            Debug = new DebugInfo
            {
                TraceId = "complex-trace-id",
                ElapsedMs = 250,
                Datasets = new Dictionary<string, object>
                {
                    ["cliente"] = new { dni = "12345678", tipoCliente = "premium" }
                },
                Derived = new Dictionary<string, object>
                {
                    ["esPremium"] = true,
                    ["prioridad"] = "alta"
                },
                InputData = request.InputData,
                Context = new Dictionary<string, object>()
            }
        };

        _mockFlowOrchestrator
            .Setup(x => x.ExecuteFlowAsync(flowName, request.InputData))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.BuildPayload(flowName, request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<OrchestratorResponse>().Subject;
        
        response.IsSuccess.Should().BeTrue();
        response.Template.Should().Be("FNB_Premium");
        response.Debug.Should().NotBeNull();
        response.Debug!.Datasets.Should().ContainKey("cliente");
        response.Debug.Derived.Should().ContainKey("esPremium");
        response.Debug.InputData.Should().ContainKey("metadata");
        
        _mockFlowOrchestrator.Verify(x => x.ExecuteFlowAsync(flowName, 
            It.Is<Dictionary<string, object>>(d => 
                d.ContainsKey("dni") && 
                d.ContainsKey("tipoCliente") && 
                d.ContainsKey("metadata"))), Times.Once);
    }
}
