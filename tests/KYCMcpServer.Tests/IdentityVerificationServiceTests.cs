using FluentAssertions;
using KYCMcpServer.Core.Interfaces;
using KYCMcpServer.Core.Models.Kyc;
using KYCMcpServer.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace KYCMcpServer.Tests;

public class IdentityVerificationServiceTests
{
    private readonly Mock<ILogger<IdentityVerificationService>> _logger = new();
    private readonly Mock<IBankingApiClient> _bankingApi = new();

    private IdentityVerificationService CreateService() =>
        new(_logger.Object, _bankingApi.Object);

    private static IdentityVerificationRequest CleanRequest(string id = "CUST001") => new()
    {
        CustomerId      = id,
        FirstName       = "Jane",
        LastName        = "Doe",
        DateOfBirth     = new DateOnly(1988, 4, 22),
        NationalityCode = "GB"
    };

    private void SetupCleanChecks()
    {
        _bankingApi.Setup(x => x.CheckPepListAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _bankingApi.Setup(x => x.CheckSanctionsListAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _bankingApi.Setup(x => x.GetWatchlistMatchesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    [Fact]
    public async Task VerifyIdentity_CleanCustomer_ReturnsVerified()
    {
        SetupCleanChecks();

        var result = await CreateService().VerifyIdentityAsync(CleanRequest());

        result.IsVerified.Should().BeTrue();
        result.VerificationStatus.Should().Be("VERIFIED");
        result.IsPepMatch.Should().BeFalse();
        result.IsSanctioned.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyIdentity_SanctionedCustomer_IsRejected()
    {
        _bankingApi.Setup(x => x.CheckPepListAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _bankingApi.Setup(x => x.CheckSanctionsListAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _bankingApi.Setup(x => x.GetWatchlistMatchesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateService().VerifyIdentityAsync(CleanRequest());

        result.IsVerified.Should().BeFalse();
        result.IsSanctioned.Should().BeTrue();
        result.VerificationStatus.Should().Be("REJECTED");
    }

    [Fact]
    public async Task VerifyIdentity_UnderageCustomer_IsRejectedImmediately()
    {
        var request = CleanRequest();
        request.DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-16));

        var result = await CreateService().VerifyIdentityAsync(request);

        result.IsVerified.Should().BeFalse();
        result.VerificationStatus.Should().Be("REJECTED");
        result.VerificationNotes.Should().Contain(n => n.Contains("under 18"));

        // Banking APIs should NOT be called for underage customers
        _bankingApi.Verify(
            x => x.CheckPepListAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task VerifyIdentity_PepMatch_FlagsResultWithNote()
    {
        _bankingApi.Setup(x => x.CheckPepListAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _bankingApi.Setup(x => x.CheckSanctionsListAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _bankingApi.Setup(x => x.GetWatchlistMatchesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateService().VerifyIdentityAsync(CleanRequest());

        result.IsPepMatch.Should().BeTrue();
        result.VerificationNotes.Should().Contain(n => n.Contains("PEP"));
    }

    [Fact]
    public async Task VerifyIdentity_WatchlistMatch_LowersScoreAndAddsNote()
    {
        SetupCleanChecks();
        _bankingApi.Setup(x => x.GetWatchlistMatchesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["INTERPOL_RED", "EU_AML"]);

        var result = await CreateService().VerifyIdentityAsync(CleanRequest());

        result.MatchedWatchlists.Should().HaveCount(2);
        result.VerificationNotes.Should().Contain(n => n.Contains("watchlist"));
    }

    [Fact]
    public async Task VerifyIdentity_ApiFailure_AddsManualReviewNote()
    {
        _bankingApi.Setup(x => x.CheckPepListAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BankingApiException("API down"));
        _bankingApi.Setup(x => x.CheckSanctionsListAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _bankingApi.Setup(x => x.GetWatchlistMatchesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateService().VerifyIdentityAsync(CleanRequest());

        result.VerificationNotes.Should().Contain(n => n.Contains("manual review"));
    }
}
