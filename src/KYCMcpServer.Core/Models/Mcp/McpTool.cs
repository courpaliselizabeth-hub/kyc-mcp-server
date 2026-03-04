using System.Text.Json.Serialization;

namespace KYCMcpServer.Core.Models.Mcp;

/// <summary>Describes a tool exposed by this MCP server.</summary>
public class McpTool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("inputSchema")]
    public McpToolSchema InputSchema { get; set; } = new();
}

public class McpToolSchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, McpToolProperty> Properties { get; set; } = [];

    [JsonPropertyName("required")]
    public List<string> Required { get; set; } = [];
}

public class McpToolProperty
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("enum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Enum { get; set; }
}
