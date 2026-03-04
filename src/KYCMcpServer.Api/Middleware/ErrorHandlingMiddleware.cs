using System.Net;
using System.Text.Json;
using KYCMcpServer.Services;

namespace KYCMcpServer.Api.Middleware;

/// <summary>
/// Global exception handler that maps domain / MCP exceptions to JSON-RPC 2.0 error responses.
/// Standard JSON-RPC error codes:
///   -32700  Parse error
///   -32600  Invalid request
///   -32601  Method not found
///   -32602  Invalid params
///   -32603  Internal error
///   -32000 to -32099  Server-defined errors
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate    _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    private readonly IHostEnvironment   _env;

    public ErrorHandlingMiddleware(
        RequestDelegate next,
        ILogger<ErrorHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next   = next;
        _logger = logger;
        _env    = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (McpToolNotFoundException ex)
        {
            await WriteErrorAsync(context, HttpStatusCode.NotFound, -32601, ex.Message);
        }
        catch (McpInvalidArgumentsException ex)
        {
            await WriteErrorAsync(context, HttpStatusCode.BadRequest, -32602, ex.Message);
        }
        catch (BankingApiException ex)
        {
            _logger.LogError(ex, "External banking API error");
            await WriteErrorAsync(context, HttpStatusCode.ServiceUnavailable, -32000,
                "An external compliance service is unavailable. Please retry or contact support.");
        }
        catch (OperationCanceledException)
        {
            await WriteErrorAsync(context, HttpStatusCode.RequestTimeout, -32000,
                "The request timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception processing request {Path}", context.Request.Path);
            var message = _env.IsDevelopment()
                ? ex.Message
                : "An internal error occurred. Please contact support.";
            await WriteErrorAsync(context, HttpStatusCode.InternalServerError, -32603, message);
        }
    }

    private static async Task WriteErrorAsync(
        HttpContext context, HttpStatusCode status, int code, string message)
    {
        if (context.Response.HasStarted) return;

        context.Response.StatusCode  = (int)status;
        context.Response.ContentType = "application/json";

        var body = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id      = (object?)null,
            error   = new { code, message }
        });

        await context.Response.WriteAsync(body);
    }
}
