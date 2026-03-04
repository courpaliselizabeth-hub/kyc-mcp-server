using System.Text.Json;
using System.Text.Json.Serialization;

namespace KYCMcpServer.Core.Models.Mcp;

/// <summary>JSON-RPC 2.0 response as per the Model Context Protocol specification.</summary>
public class McpResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public JsonElement? Id { get; set; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public McpError? Error { get; set; }

    public static McpResponse Success(JsonElement? id, object result) => new()
    {
        Id = id,
        Result = result
    };

    public static McpResponse Failure(JsonElement? id, int code, string message, object? data = null) => new()
    {
        Id = id,
        Error = new McpError { Code = code, Message = message, Data = data }
    };
}

public class McpError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }
}
