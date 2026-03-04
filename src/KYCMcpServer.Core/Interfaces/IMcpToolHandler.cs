using System.Text.Json;
using KYCMcpServer.Core.Models.Mcp;

namespace KYCMcpServer.Core.Interfaces;

public interface IMcpToolHandler
{
    IReadOnlyList<McpTool> GetAvailableTools();

    Task<object> HandleToolCallAsync(
        string toolName,
        JsonElement? arguments,
        CancellationToken cancellationToken = default);
}
