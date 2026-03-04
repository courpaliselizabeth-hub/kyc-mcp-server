namespace KYCMcpServer.Core.Models.Kyc;

public enum CustomerType
{
    Individual,
    Corporate,
    Trust,
    Partnership
}

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public class RiskAssessmentRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public CustomerType CustomerType { get; set; }
    public string CountryOfResidence { get; set; } = string.Empty;
    public string CountryOfOrigin { get; set; } = string.Empty;
    public string? BusinessType { get; set; }
    public decimal? ExpectedAnnualTransactionVolume { get; set; }
    public int? ExpectedMonthlyTransactionCount { get; set; }
    public string? SourceOfFunds { get; set; }
    public string? SourceOfWealth { get; set; }
    public bool HasPreviousKycRecord { get; set; }
    public DateTime? PreviousKycDate { get; set; }
}

public class RiskAssessmentResult
{
    public string CustomerId { get; set; } = string.Empty;
    public RiskLevel RiskLevel { get; set; }

    /// <summary>Score from 0 (no risk) to 100 (critical risk).</summary>
    public int RiskScore { get; set; }
    public string RiskCategory { get; set; } = string.Empty;
    public bool RequiresEnhancedDueDiligence { get; set; }
    public bool RequiresManualReview { get; set; }
    public List<RiskFactor> RiskFactors { get; set; } = [];
    public List<string> RecommendedActions { get; set; } = [];
    public DateTime AssessedAt { get; set; }
    public string? ReferenceId { get; set; }
}

public class RiskFactor
{
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public int WeightContribution { get; set; }
}
