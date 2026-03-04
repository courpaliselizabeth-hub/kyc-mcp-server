using KYCMcpServer.Core.Models.Kyc;

namespace KYCMcpServer.Core.Interfaces;

/// <summary>
/// Abstraction over external banking / compliance APIs.
/// Swap the implementation to integrate any real provider (e.g. Jumio, Onfido, LexisNexis).
/// </summary>
public interface IBankingApiClient
{
    Task<bool> CheckDocumentBlacklistAsync(
        string documentNumber,
        string countryCode,
        CancellationToken cancellationToken = default);

    Task<bool> CheckSanctionsListAsync(
        string firstName,
        string lastName,
        string nationalityCode,
        CancellationToken cancellationToken = default);

    Task<bool> CheckPepListAsync(
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        CancellationToken cancellationToken = default);

    Task<List<string>> GetWatchlistMatchesAsync(
        string customerId,
        CancellationToken cancellationToken = default);
}
