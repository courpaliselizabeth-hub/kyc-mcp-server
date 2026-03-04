using KYCMcpServer.Core.Models.Kyc;

namespace KYCMcpServer.Core.Interfaces;

public interface IDocumentValidationService
{
    Task<DocumentValidationResult> ValidateDocumentAsync(
        DocumentValidationRequest request,
        CancellationToken cancellationToken = default);
}
