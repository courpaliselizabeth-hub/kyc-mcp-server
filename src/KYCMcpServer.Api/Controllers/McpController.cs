using System.Text.Json;
using KYCMcpServer.Core.Interfaces;
using KYCMcpServer.Core.Models.Mcp;
using Microsoft.AspNetCore.Mvc;

namespace KYCMcpServer.Api.Controllers;

/// <summary>
/// Main MCP endpoint implementing the JSON-RPC 2.0 transport for the Model Context Protocol.
/// Protocol version: 2024-11-05
/// </summary>
[ApiController]
[Route("")]
[Produces("application/json")]
public class McpController : ControllerBase
{
    private readonly IMcpToolHandler _toolHandler;
    private readonly ILogger<McpController> _logger;

    private const string McpProtocolVersion = "2024-11-05";

    public McpController(IMcpToolHandler toolHandler, ILogger<McpController> logger)
    {
        _toolHandler = toolHandler;
        _logger      = logger;
    }

    /// <summary>
    /// MCP JSON-RPC endpoint. Accepts initialize, tools/list, tools/call, and ping methods.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> HandleRequest(
        [FromBody] McpRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("MCP request received: method={Method} id={Id}", request.Method, request.Id);

        var response = request.Method switch
        {
            "initialize" => HandleInitialize(request),
            "tools/list" => HandleToolsList(request),
            "tools/call" => await HandleToolsCallAsync(request, cancellationToken),
            "ping"       => McpResponse.Success(request.Id, new { }),
            _            => McpResponse.Failure(request.Id, -32601, $"Method not found: '{request.Method}'")
        };

        return Ok(response);
    }

    /// <summary>Health check / liveness probe.</summary>
    [HttpGet("health")]
    public IActionResult Health() => Ok(new
    {
        status    = "healthy",
        service   = "KYC MCP Server",
        version   = McpProtocolVersion,
        timestamp = DateTime.UtcNow
    });

    /// <summary>REST convenience endpoint — lists all available KYC tools.</summary>
    [HttpGet("tools")]
    public IActionResult GetTools() => Ok(new { tools = _toolHandler.GetAvailableTools() });

    // ── Private handlers ──────────────────────────────────────────────────────

    private static McpResponse HandleInitialize(McpRequest request) =>
        McpResponse.Success(request.Id, new
        {
            protocolVersion = McpProtocolVersion,
            capabilities    = new { tools = new { listChanged = false } },
            serverInfo      = new { name = "kyc-mcp-server", version = "1.0.0" }
        });

    private McpResponse HandleToolsList(McpRequest request) =>
        McpResponse.Success(request.Id, new { tools = _toolHandler.GetAvailableTools() });

    private async Task<McpResponse> HandleToolsCallAsync(McpRequest request, CancellationToken ct)
    {
        if (request.Params is null)
            return McpResponse.Failure(request.Id, -32602, "'params' are required for tools/call.");

        request.Params.Value.TryGetProperty("name",      out var nameProp);
        request.Params.Value.TryGetProperty("arguments", out var argsProp);

        var toolName = nameProp.ValueKind == JsonValueKind.String ? nameProp.GetString() : null;
        if (string.IsNullOrEmpty(toolName))
            return McpResponse.Failure(request.Id, -32602, "Tool 'name' is required in params.");

        var arguments = argsProp.ValueKind != JsonValueKind.Undefined
            ? (JsonElement?)argsProp
            : null;

        var result = await _toolHandler.HandleToolCallAsync(toolName, arguments, ct);
        return McpResponse.Success(request.Id, result);
    }
}
