using System.Text.Json;
using System.Text.Json.Serialization;

namespace KYCMcpServer.Core.Models.Mcp;

/// <summary>JSON-RPC 2.0 request as per the Model Context Protocol specification.</summary>
public class McpRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public JsonElement? Id { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }
}
