using KYCMcpServer.Core.Interfaces;
using KYCMcpServer.Core.Models.Kyc;
using Microsoft.Extensions.Logging;

namespace KYCMcpServer.Services;

public class RiskAssessmentService : IRiskAssessmentService
{
    private readonly ILogger<RiskAssessmentService> _logger;

    // FATF high-risk / sanctioned jurisdictions (simplified — update from FATF website in production)
    private static readonly HashSet<string> HighRiskCountries =
        new(StringComparer.OrdinalIgnoreCase) { "IR", "KP", "MM", "SY", "BY", "CU", "SD", "SO", "YE" };

    // FATF grey-list / elevated-risk jurisdictions
    private static readonly HashSet<string> MediumRiskCountries =
        new(StringComparer.OrdinalIgnoreCase) { "AF", "HT", "VU", "PA", "BB", "JM", "TT", "PK", "NG", "ML" };

    private static readonly HashSet<string> HighRiskFundsSources =
        new(StringComparer.OrdinalIgnoreCase) { "cryptocurrency", "crypto", "gambling", "casino", "cash", "unknown" };

    public RiskAssessmentService(ILogger<RiskAssessmentService> logger)
    {
        _logger = logger;
    }

    public async Task<RiskAssessmentResult> AssessRiskAsync(
        RiskAssessmentRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting risk assessment for {CustomerId} ({CustomerType})",
            request.CustomerId, request.CustomerType);

        var factors = new List<RiskFactor>();
        var actions = new List<string>();

        int score = 0;
        score += AssessCountryRisk(request.CountryOfResidence, request.CountryOfOrigin, factors);
        score += AssessCustomerTypeRisk(request.CustomerType, factors);
        score += AssessTransactionVolumeRisk(request.ExpectedAnnualTransactionVolume,
                                             request.ExpectedMonthlyTransactionCount, factors);
        score += AssessSourceOfFundsRisk(request.SourceOfFunds, factors);
        score += AssessKycHistoryRisk(request.HasPreviousKycRecord, request.PreviousKycDate, factors);

        var riskScore = Math.Min(100, score);
        var riskLevel = DetermineRiskLevel(riskScore);
        var requiresEdd    = riskLevel is RiskLevel.High or RiskLevel.Critical;
        var requiresManual = riskLevel is RiskLevel.Critical;

        BuildRecommendations(actions, riskLevel, requiresEdd, requiresManual,
                             request.CountryOfResidence);

        var result = new RiskAssessmentResult
        {
            CustomerId                  = request.CustomerId,
            RiskLevel                   = riskLevel,
            RiskScore                   = riskScore,
            RiskCategory                = DetermineCategory(request.CustomerType, request.BusinessType),
            RequiresEnhancedDueDiligence = requiresEdd,
            RequiresManualReview        = requiresManual,
            RiskFactors                 = factors,
            RecommendedActions          = actions,
            AssessedAt                  = DateTime.UtcNow,
            ReferenceId                 = GenerateReferenceId()
        };

        _logger.LogInformation(
            "Risk assessment complete for {CustomerId}: {Level} (score: {Score}, EDD: {Edd}, ref: {Ref})",
            request.CustomerId, riskLevel, riskScore, requiresEdd, result.ReferenceId);

        await Task.CompletedTask; // Placeholder for future async enrichment (e.g. real-time sanctions feeds)
        return result;
    }

    private int AssessCountryRisk(string residence, string origin, List<RiskFactor> factors)
    {
        int score = 0;

        if (HighRiskCountries.Contains(residence))
        {
            score += 40;
            factors.Add(Factor("Geographic",
                $"Country of residence ({residence}) is a FATF-listed high-risk jurisdiction.", "High", 40));
        }
        else if (MediumRiskCountries.Contains(residence))
        {
            score += 20;
            factors.Add(Factor("Geographic",
                $"Country of residence ({residence}) is on the FATF grey list.", "Medium", 20));
        }

        if (!string.Equals(origin, residence, StringComparison.OrdinalIgnoreCase)
            && HighRiskCountries.Contains(origin))
        {
            score += 15;
            factors.Add(Factor("Geographic",
                $"Country of origin ({origin}) is a FATF-listed high-risk jurisdiction.", "Medium", 15));
        }

        return score;
    }

    private static int AssessCustomerTypeRisk(CustomerType type, List<RiskFactor> factors) =>
        type switch
        {
            CustomerType.Individual  => 0,
            CustomerType.Corporate   => AddFactor(factors, "Entity Type",
                "Corporate entity — beneficial ownership verification required.", "Low", 10),
            CustomerType.Trust       => AddFactor(factors, "Entity Type",
                "Trust structure — complex ownership requiring enhanced scrutiny.", "Medium", 20),
            CustomerType.Partnership => AddFactor(factors, "Entity Type",
                "Partnership — all partners must be individually verified.", "Low", 15),
            _                        => 0
        };

    private static int AssessTransactionVolumeRisk(decimal? annualVolume, int? monthlyCount,
        List<RiskFactor> factors)
    {
        int score = 0;

        if (annualVolume > 1_000_000)
            score += AddFactor(factors, "Transaction Volume",
                $"High annual transaction volume (${annualVolume:N0}).", "High", 20);
        else if (annualVolume > 100_000)
            score += AddFactor(factors, "Transaction Volume",
                $"Elevated annual transaction volume (${annualVolume:N0}).", "Medium", 10);

        if (monthlyCount > 500)
            score += AddFactor(factors, "Transaction Frequency",
                $"High monthly transaction count ({monthlyCount}/month).", "Medium", 10);

        return score;
    }

    private static int AssessSourceOfFundsRisk(string? sourceOfFunds, List<RiskFactor> factors)
    {
        if (string.IsNullOrWhiteSpace(sourceOfFunds))
            return AddFactor(factors, "Source of Funds",
                "Source of funds not declared — required for compliance.", "Medium", 15);

        if (HighRiskFundsSources.Any(s => sourceOfFunds.Contains(s, StringComparison.OrdinalIgnoreCase)))
            return AddFactor(factors, "Source of Funds",
                $"Elevated-risk source of funds: \"{sourceOfFunds}\".", "High", 20);

        return 0;
    }

    private static int AssessKycHistoryRisk(bool hasPrevious, DateTime? previousDate,
        List<RiskFactor> factors)
    {
        if (!hasPrevious || !previousDate.HasValue) return 0;

        var days = (DateTime.UtcNow - previousDate.Value).Days;
        if (days > 365)
            return AddFactor(factors, "KYC History",
                $"Existing KYC record is outdated ({days} days old) — refresh required.", "Low", 5);

        return 0;
    }

    private static RiskLevel DetermineRiskLevel(int score) => score switch
    {
        < 20 => RiskLevel.Low,
        < 40 => RiskLevel.Medium,
        < 70 => RiskLevel.High,
        _    => RiskLevel.Critical
    };

    private static string DetermineCategory(CustomerType type, string? businessType) =>
        type switch
        {
            CustomerType.Individual  => "Retail",
            CustomerType.Corporate   => string.IsNullOrEmpty(businessType) ? "Corporate" : $"Corporate/{businessType}",
            CustomerType.Trust       => "Trust & Fiduciary",
            CustomerType.Partnership => "Business Partnership",
            _                        => "Unknown"
        };

    private static void BuildRecommendations(List<string> actions, RiskLevel level,
        bool requiresEdd, bool requiresManual, string countryOfResidence)
    {
        if (requiresEdd)    actions.Add("Perform Enhanced Due Diligence (EDD) screening.");
        if (requiresManual) actions.Add("Escalate to Compliance Officer for manual review.");
        if (HighRiskCountries.Contains(countryOfResidence))
            actions.Add("Obtain Senior Management Approval before onboarding.");

        actions.Add(level switch
        {
            RiskLevel.Low    => "Standard monitoring — annual KYC refresh required.",
            RiskLevel.Medium => "Ongoing transaction monitoring — 6-month KYC refresh required.",
            RiskLevel.High   => "Heightened monitoring — quarterly KYC refresh required.",
            _                => "Maximum monitoring — monthly compliance review required."
        });
    }

    private static int AddFactor(List<RiskFactor> factors, string category,
        string description, string severity, int weight)
    {
        factors.Add(new RiskFactor
        {
            Category          = category,
            Description       = description,
            Severity          = severity,
            WeightContribution = weight
        });
        return weight;
    }

    private static RiskFactor Factor(string category, string description, string severity, int weight) =>
        new() { Category = category, Description = description, Severity = severity, WeightContribution = weight };

    private static string GenerateReferenceId() =>
        Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
}
