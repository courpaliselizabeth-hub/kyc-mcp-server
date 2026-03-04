using KYCMcpServer.Core.Models.Kyc;

namespace KYCMcpServer.Core.Interfaces;

public interface IRiskAssessmentService
{
    Task<RiskAssessmentResult> AssessRiskAsync(
        RiskAssessmentRequest request,
        CancellationToken cancellationToken = default);
}
