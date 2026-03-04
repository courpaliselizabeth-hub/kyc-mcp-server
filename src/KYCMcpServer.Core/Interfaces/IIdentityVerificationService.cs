using KYCMcpServer.Core.Models.Kyc;

namespace KYCMcpServer.Core.Interfaces;

public interface IIdentityVerificationService
{
    Task<IdentityVerificationResult> VerifyIdentityAsync(
        IdentityVerificationRequest request,
        CancellationToken cancellationToken = default);
}
