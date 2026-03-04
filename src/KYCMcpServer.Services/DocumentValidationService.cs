using System.Text.RegularExpressions;
using KYCMcpServer.Core.Interfaces;
using KYCMcpServer.Core.Models.Kyc;
using Microsoft.Extensions.Logging;

namespace KYCMcpServer.Services;

public class DocumentValidationService : IDocumentValidationService
{
    private readonly ILogger<DocumentValidationService> _logger;
    private readonly IBankingApiClient _bankingApiClient;

    private static readonly Dictionary<DocumentType, Regex> DocumentPatterns = new()
    {
        [DocumentType.Passport]        = new Regex(@"^[A-Z0-9]{6,9}$",  RegexOptions.Compiled),
        [DocumentType.DriversLicense]  = new Regex(@"^[A-Z0-9]{5,15}$", RegexOptions.Compiled),
        [DocumentType.NationalId]      = new Regex(@"^[A-Z0-9]{6,15}$", RegexOptions.Compiled),
        [DocumentType.ResidencePermit] = new Regex(@"^[A-Z0-9]{6,15}$", RegexOptions.Compiled),
        [DocumentType.UtilityBill]     = new Regex(@".+",                RegexOptions.Compiled),
        [DocumentType.BankStatement]   = new Regex(@".+",                RegexOptions.Compiled),
    };

    public DocumentValidationService(
        ILogger<DocumentValidationService> logger,
        IBankingApiClient bankingApiClient)
    {
        _logger = logger;
        _bankingApiClient = bankingApiClient;
    }

    public async Task<DocumentValidationResult> ValidateDocumentAsync(
        DocumentValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting document validation for customer {CustomerId}, type {DocumentType}",
            request.CustomerId, request.DocumentType);

        var errors = new List<string>();
        var result = new DocumentValidationResult
        {
            DocumentType  = request.DocumentType.ToString(),
            DocumentNumber = MaskDocumentNumber(request.DocumentNumber),
            CountryCode   = request.CountryCode,
            ValidatedAt   = DateTime.UtcNow,
            ReferenceId   = GenerateReferenceId()
        };

        // Format validation
        if (!ValidateFormat(request.DocumentType, request.DocumentNumber))
            errors.Add($"Invalid document number format for {request.DocumentType}.");

        // Country code validation (must be 2-letter ISO)
        if (string.IsNullOrWhiteSpace(request.CountryCode) || request.CountryCode.Length != 2)
            errors.Add("Invalid country code — must be a 2-letter ISO 3166-1 alpha-2 code.");

        // Expiry check
        if (request.ExpiryDate.HasValue)
        {
            result.IsExpired = request.ExpiryDate.Value < DateOnly.FromDateTime(DateTime.UtcNow);
            if (result.IsExpired)
                errors.Add($"Document expired on {request.ExpiryDate.Value:yyyy-MM-dd}.");
        }

        // Blacklist check (only if format passes to reduce unnecessary API calls)
        if (errors.Count == 0)
        {
            try
            {
                result.IsBlacklisted = await _bankingApiClient.CheckDocumentBlacklistAsync(
                    request.DocumentNumber, request.CountryCode, cancellationToken);

                if (result.IsBlacklisted)
                    errors.Add("Document is flagged on the blacklist database.");
            }
            catch (BankingApiException ex)
            {
                _logger.LogWarning(ex, "Blacklist check failed (ref: {ReferenceId}) — flagging for manual review",
                    result.ReferenceId);
                errors.Add("Blacklist check service unavailable — manual verification required.");
            }
        }

        result.ValidationErrors = errors;
        result.IsValid          = errors.Count == 0;
        result.ConfidenceScore  = CalculateConfidenceScore(result);
        result.ValidationStatus = result.IsValid ? "PASSED" : "FAILED";

        _logger.LogInformation(
            "Document validation complete for {CustomerId}: {Status} (confidence: {Score:P0}, ref: {ReferenceId})",
            request.CustomerId, result.ValidationStatus, result.ConfidenceScore, result.ReferenceId);

        return result;
    }

    private static bool ValidateFormat(DocumentType type, string number)
    {
        if (string.IsNullOrWhiteSpace(number)) return false;
        return DocumentPatterns.TryGetValue(type, out var regex) && regex.IsMatch(number.ToUpperInvariant());
    }

    private static string MaskDocumentNumber(string number)
    {
        if (string.IsNullOrEmpty(number) || number.Length <= 4)
            return new string('*', number?.Length ?? 0);
        return string.Concat(new string('*', number.Length - 4), number[^4..]);
    }

    private static double CalculateConfidenceScore(DocumentValidationResult result)
    {
        double score = 1.0;
        score -= result.ValidationErrors.Count * 0.2;
        if (result.IsExpired)     score -= 0.3;
        if (result.IsBlacklisted) score -= 0.5;
        return Math.Clamp(score, 0, 1);
    }

    private static string GenerateReferenceId() =>
        Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
}
