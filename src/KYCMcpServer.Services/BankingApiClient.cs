using System.Net.Http.Json;
using KYCMcpServer.Core.Interfaces;
using KYCMcpServer.Core.Models.Kyc;
using Microsoft.Extensions.Logging;

namespace KYCMcpServer.Services;

/// <summary>
/// HTTP client for external banking / compliance APIs.
/// Replace the endpoint paths to match your actual provider (Jumio, Onfido, LexisNexis, etc.).
/// </summary>
public class BankingApiClient : IBankingApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BankingApiClient> _logger;

    public BankingApiClient(HttpClient httpClient, ILogger<BankingApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> CheckDocumentBlacklistAsync(
        string documentNumber,
        string countryCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<BlacklistResponse>(
                $"v1/documents/blacklist?number={Uri.EscapeDataString(documentNumber)}&country={countryCode}",
                cancellationToken);

            return response?.IsBlacklisted ?? false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Document blacklist API unreachable");
            throw new BankingApiException("Document blacklist service unavailable", ex);
        }
    }

    public async Task<bool> CheckSanctionsListAsync(
        string firstName,
        string lastName,
        string nationalityCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new { first_name = firstName, last_name = lastName, nationality = nationalityCode };
            var response = await _httpClient.PostAsJsonAsync("v1/sanctions/check", payload, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<SanctionsResponse>(cancellationToken: cancellationToken);
            return result?.IsSanctioned ?? false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Sanctions list API unavailable");
            throw new BankingApiException("Sanctions list service unavailable", ex);
        }
    }

    public async Task<bool> CheckPepListAsync(
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                first_name = firstName,
                last_name = lastName,
                date_of_birth = dateOfBirth.ToString("yyyy-MM-dd")
            };
            var response = await _httpClient.PostAsJsonAsync("v1/pep/check", payload, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<PepResponse>(cancellationToken: cancellationToken);
            return result?.IsPep ?? false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "PEP check API unavailable");
            throw new BankingApiException("PEP check service unavailable", ex);
        }
    }

    public async Task<List<string>> GetWatchlistMatchesAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<WatchlistResponse>(
                $"v1/watchlists/{Uri.EscapeDataString(customerId)}",
                cancellationToken);

            return response?.MatchedLists ?? [];
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Watchlist API unavailable for customer {CustomerId}", customerId);
            throw new BankingApiException("Watchlist service unavailable", ex);
        }
    }

    // Internal response DTOs — kept private, not part of the public domain model
    private sealed record BlacklistResponse(bool IsBlacklisted);
    private sealed record SanctionsResponse(bool IsSanctioned, string? MatchedEntry = null);
    private sealed record PepResponse(bool IsPep, string? MatchedEntry = null);
    private sealed record WatchlistResponse(List<string> MatchedLists);
}

public class BankingApiException : Exception
{
    public BankingApiException(string message, Exception? inner = null) : base(message, inner) { }
}
