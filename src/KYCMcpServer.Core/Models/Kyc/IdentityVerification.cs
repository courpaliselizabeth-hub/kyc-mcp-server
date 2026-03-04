namespace KYCMcpServer.Core.Models.Kyc;

public class IdentityVerificationRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string NationalityCode { get; set; } = string.Empty;
    public string? TaxId { get; set; }
    public string? EmailAddress { get; set; }
    public string? PhoneNumber { get; set; }
    public AddressInfo? Address { get; set; }
}

public class AddressInfo
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
}

public class IdentityVerificationResult
{
    public string CustomerId { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public string VerificationStatus { get; set; } = string.Empty;
    public double IdentityMatchScore { get; set; }

    /// <summary>Politically Exposed Person match.</summary>
    public bool IsPepMatch { get; set; }
    public bool IsSanctioned { get; set; }
    public bool IsAdverseMedia { get; set; }
    public List<string> MatchedWatchlists { get; set; } = [];
    public List<string> VerificationNotes { get; set; } = [];
    public DateTime VerifiedAt { get; set; }
    public string? ReferenceId { get; set; }
}
