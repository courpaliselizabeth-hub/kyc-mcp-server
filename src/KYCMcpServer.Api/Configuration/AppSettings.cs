namespace KYCMcpServer.Api.Configuration;

/// <summary>
/// Strongly-typed configuration bound from appsettings.json and overrideable via environment variables.
/// See .env.example for the full list of supported variables.
/// </summary>
public class AppSettings
{
    // Banking / Compliance API credentials
    public string? BankingApiKey { get; set; }
    public string  BankingApiBaseUrl { get; set; } = "https://api.banking.example.com/";

    public string? IdentityVerificationApiKey { get; set; }
    public string  IdentityVerificationApiBaseUrl { get; set; } = "https://api.identity.example.com/";

    public string? DocumentVerificationApiKey { get; set; }
    public string  DocumentVerificationApiBaseUrl { get; set; } = "https://api.documents.example.com/";

    public string? AmlApiKey { get; set; }
    public string  AmlApiBaseUrl { get; set; } = "https://api.aml.example.com/";

    // Behaviour
    public int  RequestTimeoutSeconds { get; set; } = 30;
    public bool EnableDetailedErrors  { get; set; } = false;
    public string McpServerVersion    { get; set; } = "1.0.0";
}
