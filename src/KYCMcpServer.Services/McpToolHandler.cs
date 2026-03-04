using System.Text.Json;
using System.Text.Json.Serialization;
using KYCMcpServer.Core.Interfaces;
using KYCMcpServer.Core.Models.Kyc;
using KYCMcpServer.Core.Models.Mcp;
using Microsoft.Extensions.Logging;

namespace KYCMcpServer.Services;

public class McpToolHandler : IMcpToolHandler
{
    private readonly ILogger<McpToolHandler> _logger;
    private readonly IDocumentValidationService _documentValidation;
    private readonly IIdentityVerificationService _identityVerification;
    private readonly IRiskAssessmentService _riskAssessment;

    private static readonly JsonSerializerOptions ResultJsonOptions = new()
    {
        WriteIndented = true,
        Converters    = { new JsonStringEnumConverter() }
    };

    public McpToolHandler(
        ILogger<McpToolHandler> logger,
        IDocumentValidationService documentValidation,
        IIdentityVerificationService identityVerification,
        IRiskAssessmentService riskAssessment)
    {
        _logger               = logger;
        _documentValidation   = documentValidation;
        _identityVerification = identityVerification;
        _riskAssessment       = riskAssessment;
    }

    public IReadOnlyList<McpTool> GetAvailableTools() =>
    [
        new McpTool
        {
            Name        = "validate_document",
            Description = "Validates a KYC identity document (passport, driver's license, national ID, etc.) " +
                          "by checking format correctness, expiry status, and blacklist database.",
            InputSchema = new McpToolSchema
            {
                Properties = new Dictionary<string, McpToolProperty>
                {
                    ["customer_id"]           = Prop("Unique customer identifier."),
                    ["document_type"]         = Prop("Document type.", Enum.GetNames<DocumentType>().ToList()),
                    ["document_number"]       = Prop("The document number / ID."),
                    ["country_code"]          = Prop("2-letter ISO 3166-1 alpha-2 issuing country code."),
                    ["expiry_date"]           = Prop("Document expiry date in YYYY-MM-DD format (optional)."),
                    ["issuing_authority"]     = Prop("Authority that issued the document (optional)."),
                    ["document_image_base64"] = Prop("Base64-encoded document image for OCR validation (optional).")
                },
                Required = ["customer_id", "document_type", "document_number", "country_code"]
            }
        },
        new McpTool
        {
            Name        = "verify_identity",
            Description = "Verifies a customer's identity against global compliance databases including " +
                          "PEP lists, sanctions lists, and adverse-media watchlists.",
            InputSchema = new McpToolSchema
            {
                Properties = new Dictionary<string, McpToolProperty>
                {
                    ["customer_id"]      = Prop("Unique customer identifier."),
                    ["first_name"]       = Prop("Customer's first/given name."),
                    ["last_name"]        = Prop("Customer's last/family name."),
                    ["date_of_birth"]    = Prop("Date of birth in YYYY-MM-DD format."),
                    ["nationality_code"] = Prop("2-letter ISO 3166-1 alpha-2 nationality code."),
                    ["tax_id"]           = Prop("Tax identification number (optional)."),
                    ["email_address"]    = Prop("Customer email address (optional)."),
                    ["phone_number"]     = Prop("Customer phone number in E.164 format (optional).")
                },
                Required = ["customer_id", "first_name", "last_name", "date_of_birth", "nationality_code"]
            }
        },
        new McpTool
        {
            Name        = "assess_risk",
            Description = "Performs AML/KYC risk scoring for a customer based on jurisdiction, " +
                          "entity type, transaction profile, and source of funds.",
            InputSchema = new McpToolSchema
            {
                Properties = new Dictionary<string, McpToolProperty>
                {
                    ["customer_id"]                           = Prop("Unique customer identifier."),
                    ["customer_type"]                         = Prop("Entity type.", Enum.GetNames<CustomerType>().ToList()),
                    ["country_of_residence"]                  = Prop("2-letter ISO country code of residence."),
                    ["country_of_origin"]                     = Prop("2-letter ISO country code of origin."),
                    ["business_type"]                         = Prop("Nature of the customer's business (optional)."),
                    ["expected_annual_transaction_volume"]    = Prop("Expected annual USD transaction volume (optional)."),
                    ["expected_monthly_transaction_count"]    = Prop("Expected number of transactions per month (optional)."),
                    ["source_of_funds"]                       = Prop("Primary source of funds (e.g. salary, investments)."),
                    ["source_of_wealth"]                      = Prop("Primary source of wealth (optional).")
                },
                Required = ["customer_id", "customer_type", "country_of_residence", "country_of_origin"]
            }
        }
    ];

    public async Task<object> HandleToolCallAsync(
        string toolName,
        JsonElement? arguments,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling MCP tool call: {ToolName}", toolName);

        return toolName switch
        {
            "validate_document" => await HandleValidateDocumentAsync(arguments, cancellationToken),
            "verify_identity"   => await HandleVerifyIdentityAsync(arguments, cancellationToken),
            "assess_risk"       => await HandleAssessRiskAsync(arguments, cancellationToken),
            _                   => throw new McpToolNotFoundException($"Unknown tool: '{toolName}'. " +
                                       "Call tools/list to see available tools.")
        };
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private async Task<object> HandleValidateDocumentAsync(JsonElement? args, CancellationToken ct)
    {
        RequireArgs(args, "validate_document");

        if (!Enum.TryParse<DocumentType>(RequiredString(args!.Value, "document_type"), ignoreCase: true, out var docType))
            throw new McpInvalidArgumentsException("Invalid document_type value.");

        var request = new DocumentValidationRequest
        {
            CustomerId            = RequiredString(args.Value, "customer_id"),
            DocumentType          = docType,
            DocumentNumber        = RequiredString(args.Value, "document_number"),
            CountryCode           = RequiredString(args.Value, "country_code").ToUpperInvariant(),
            IssuingAuthority      = OptionalString(args.Value, "issuing_authority"),
            DocumentImageBase64   = OptionalString(args.Value, "document_image_base64")
        };

        var expiryStr = OptionalString(args.Value, "expiry_date");
        if (!string.IsNullOrEmpty(expiryStr))
        {
            if (!DateOnly.TryParse(expiryStr, out var expiry))
                throw new McpInvalidArgumentsException("expiry_date must be in YYYY-MM-DD format.");
            request.ExpiryDate = expiry;
        }

        return WrapResult(await _documentValidation.ValidateDocumentAsync(request, ct));
    }

    private async Task<object> HandleVerifyIdentityAsync(JsonElement? args, CancellationToken ct)
    {
        RequireArgs(args, "verify_identity");

        if (!DateOnly.TryParse(RequiredString(args!.Value, "date_of_birth"), out var dob))
            throw new McpInvalidArgumentsException("date_of_birth must be in YYYY-MM-DD format.");

        var request = new IdentityVerificationRequest
        {
            CustomerId      = RequiredString(args.Value, "customer_id"),
            FirstName       = RequiredString(args.Value, "first_name"),
            LastName        = RequiredString(args.Value, "last_name"),
            DateOfBirth     = dob,
            NationalityCode = RequiredString(args.Value, "nationality_code").ToUpperInvariant(),
            TaxId           = OptionalString(args.Value, "tax_id"),
            EmailAddress    = OptionalString(args.Value, "email_address"),
            PhoneNumber     = OptionalString(args.Value, "phone_number")
        };

        return WrapResult(await _identityVerification.VerifyIdentityAsync(request, ct));
    }

    private async Task<object> HandleAssessRiskAsync(JsonElement? args, CancellationToken ct)
    {
        RequireArgs(args, "assess_risk");

        if (!Enum.TryParse<CustomerType>(RequiredString(args!.Value, "customer_type"), ignoreCase: true, out var custType))
            throw new McpInvalidArgumentsException("Invalid customer_type value.");

        var request = new RiskAssessmentRequest
        {
            CustomerId          = RequiredString(args.Value, "customer_id"),
            CustomerType        = custType,
            CountryOfResidence  = RequiredString(args.Value, "country_of_residence").ToUpperInvariant(),
            CountryOfOrigin     = RequiredString(args.Value, "country_of_origin").ToUpperInvariant(),
            BusinessType        = OptionalString(args.Value, "business_type"),
            SourceOfFunds       = OptionalString(args.Value, "source_of_funds"),
            SourceOfWealth      = OptionalString(args.Value, "source_of_wealth")
        };

        if (args.Value.TryGetProperty("expected_annual_transaction_volume", out var vol))
            request.ExpectedAnnualTransactionVolume = vol.GetDecimal();
        if (args.Value.TryGetProperty("expected_monthly_transaction_count", out var cnt))
            request.ExpectedMonthlyTransactionCount = cnt.GetInt32();

        return WrapResult(await _riskAssessment.AssessRiskAsync(request, ct));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Wraps a result object into the MCP content envelope.</summary>
    private static object WrapResult(object result) => new
    {
        content = new[]
        {
            new { type = "text", text = JsonSerializer.Serialize(result, ResultJsonOptions) }
        }
    };

    private static McpToolProperty Prop(string description, List<string>? @enum = null) =>
        new() { Type = "string", Description = description, Enum = @enum };

    private static void RequireArgs(JsonElement? args, string toolName)
    {
        if (args is null)
            throw new McpInvalidArgumentsException($"Arguments object is required for '{toolName}'.");
    }

    private static string RequiredString(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null)
            throw new McpInvalidArgumentsException($"Required argument '{name}' is missing.");
        return prop.GetString()
               ?? throw new McpInvalidArgumentsException($"Argument '{name}' must be a non-null string.");
    }

    private static string? OptionalString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var prop) ? prop.GetString() : null;
}

// ── Exceptions ────────────────────────────────────────────────────────────────

public class McpToolNotFoundException : Exception
{
    public McpToolNotFoundException(string message) : base(message) { }
}

public class McpInvalidArgumentsException : Exception
{
    public McpInvalidArgumentsException(string message) : base(message) { }
}
