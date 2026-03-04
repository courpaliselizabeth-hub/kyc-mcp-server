namespace KYCMcpServer.Core.Models.Kyc;

public enum DocumentType
{
    Passport,
    DriversLicense,
    NationalId,
    ResidencePermit,
    UtilityBill,
    BankStatement
}

public class DocumentValidationRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public DocumentType DocumentType { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string? IssuingAuthority { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? DocumentImageBase64 { get; set; }
}

public class DocumentValidationResult
{
    public bool IsValid { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public bool IsExpired { get; set; }
    public bool IsBlacklisted { get; set; }
    public string ValidationStatus { get; set; } = string.Empty;
    public List<string> ValidationErrors { get; set; } = [];
    public double ConfidenceScore { get; set; }
    public DateTime ValidatedAt { get; set; }
    public string? ReferenceId { get; set; }
}
