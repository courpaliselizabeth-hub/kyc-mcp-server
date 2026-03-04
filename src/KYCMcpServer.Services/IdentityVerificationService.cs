using KYCMcpServer.Core.Interfaces;
using KYCMcpServer.Core.Models.Kyc;
using Microsoft.Extensions.Logging;

namespace KYCMcpServer.Services;

public class IdentityVerificationService : IIdentityVerificationService
{
    private readonly ILogger<IdentityVerificationService> _logger;
    private readonly IBankingApiClient _bankingApiClient;

    public IdentityVerificationService(
        ILogger<IdentityVerificationService> logger,
        IBankingApiClient bankingApiClient)
    {
        _logger = logger;
        _bankingApiClient = bankingApiClient;
    }

    public async Task<IdentityVerificationResult> VerifyIdentityAsync(
        IdentityVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting identity verification for customer {CustomerId}", request.CustomerId);

        var notes = new List<string>();
        var result = new IdentityVerificationResult
        {
            CustomerId  = request.CustomerId,
            VerifiedAt  = DateTime.UtcNow,
            ReferenceId = GenerateReferenceId()
        };

        // Age verification — must be 18+
        var age = CalculateAge(request.DateOfBirth);
        if (age < 18)
        {
            notes.Add($"Customer is under 18 years old (age: {age}). Onboarding rejected.");
            result.VerificationStatus = "REJECTED";
            result.IsVerified         = false;
            result.VerificationNotes  = notes;
            return result;
        }

        // PEP check
        try
        {
            result.IsPepMatch = await _bankingApiClient.CheckPepListAsync(
                request.FirstName, request.LastName, request.DateOfBirth, cancellationToken);
            if (result.IsPepMatch)
                notes.Add("Subject matched on PEP (Politically Exposed Person) list — enhanced due diligence required.");
        }
        catch (BankingApiException ex)
        {
            _logger.LogWarning(ex, "PEP check unavailable for {CustomerId}", request.CustomerId);
            notes.Add("PEP check service unavailable — manual review required.");
        }

        // Sanctions check
        try
        {
            result.IsSanctioned = await _bankingApiClient.CheckSanctionsListAsync(
                request.FirstName, request.LastName, request.NationalityCode, cancellationToken);
            if (result.IsSanctioned)
                notes.Add("Subject matched on sanctions list — transaction blocked pending compliance review.");
        }
        catch (BankingApiException ex)
        {
            _logger.LogWarning(ex, "Sanctions check unavailable for {CustomerId}", request.CustomerId);
            notes.Add("Sanctions check service unavailable — manual review required.");
        }

        // Watchlist check
        try
        {
            result.MatchedWatchlists = await _bankingApiClient.GetWatchlistMatchesAsync(
                request.CustomerId, cancellationToken);
            if (result.MatchedWatchlists.Count > 0)
                notes.Add($"Subject matched in {result.MatchedWatchlists.Count} watchlist(s): " +
                          string.Join(", ", result.MatchedWatchlists));
        }
        catch (BankingApiException ex)
        {
            _logger.LogWarning(ex, "Watchlist check unavailable for {CustomerId}", request.CustomerId);
            notes.Add("Watchlist check service unavailable — manual review required.");
        }

        result.IdentityMatchScore = CalculateMatchScore(result);
        result.IsVerified         = !result.IsSanctioned && result.IdentityMatchScore >= 0.6;
        result.VerificationStatus = DetermineStatus(result);
        result.VerificationNotes  = notes;

        _logger.LogInformation(
            "Identity verification complete for {CustomerId}: {Status} (score: {Score:F2}, ref: {ReferenceId})",
            request.CustomerId, result.VerificationStatus, result.IdentityMatchScore, result.ReferenceId);

        return result;
    }

    private static int CalculateAge(DateOnly dob)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age   = today.Year - dob.Year;
        if (dob > today.AddYears(-age)) age--;
        return age;
    }

    private static double CalculateMatchScore(IdentityVerificationResult result)
    {
        double score = 1.0;
        if (result.IsPepMatch)   score -= 0.15;
        if (result.IsSanctioned) score -= 0.5;
        score -= result.MatchedWatchlists.Count * 0.1;
        return Math.Clamp(score, 0, 1);
    }

    private static string DetermineStatus(IdentityVerificationResult result) =>
        result.IsSanctioned           ? "REJECTED" :
        result.IdentityMatchScore >= 0.8 ? "VERIFIED" :
        result.IdentityMatchScore >= 0.6 ? "VERIFIED_WITH_FLAGS" :
                                           "REQUIRES_REVIEW";

    private static string GenerateReferenceId() =>
        Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
}
