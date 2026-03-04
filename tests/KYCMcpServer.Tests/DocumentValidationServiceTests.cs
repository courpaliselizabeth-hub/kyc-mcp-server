using FluentAssertions;
using KYCMcpServer.Core.Interfaces;
using KYCMcpServer.Core.Models.Kyc;
using KYCMcpServer.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace KYCMcpServer.Tests;

public class DocumentValidationServiceTests
{
    private readonly Mock<ILogger<DocumentValidationService>> _logger = new();
    private readonly Mock<IBankingApiClient> _bankingApi = new();

    private DocumentValidationService CreateService() =>
        new(_logger.Object, _bankingApi.Object);

    private static DocumentValidationRequest ValidPassportRequest(string customerId = "CUST001") => new()
    {
        CustomerId     = customerId,
        DocumentType   = DocumentType.Passport,
        DocumentNumber = "AB123456",
        CountryCode    = "US",
        ExpiryDate     = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(5))
    };

    [Fact]
    public async Task ValidateDocument_ValidPassport_ReturnsValid()
    {
        _bankingApi.Setup(x => x.CheckDocumentBlacklistAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateService().ValidateDocumentAsync(ValidPassportRequest());

        result.IsValid.Should().BeTrue();
        result.ValidationStatus.Should().Be("PASSED");
        result.ValidationErrors.Should().BeEmpty();
        result.ConfidenceScore.Should().Be(1.0);
    }

    [Fact]
    public async Task ValidateDocument_ExpiredDocument_ReturnsInvalid()
    {
        var request = ValidPassportRequest();
        request.ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1));

        var result = await CreateService().ValidateDocumentAsync(request);

        result.IsValid.Should().BeFalse();
        result.IsExpired.Should().BeTrue();
        result.ValidationErrors.Should().Contain(e => e.Contains("expired"));
    }

    [Fact]
    public async Task ValidateDocument_BlacklistedDocument_ReturnsInvalid()
    {
        _bankingApi.Setup(x => x.CheckDocumentBlacklistAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateService().ValidateDocumentAsync(ValidPassportRequest());

        result.IsValid.Should().BeFalse();
        result.IsBlacklisted.Should().BeTrue();
        result.ValidationErrors.Should().Contain(e => e.Contains("blacklist"));
    }

    [Theory]
    [InlineData("",         "US")] // empty doc number
    [InlineData("AB123456", "")]   // empty country
    [InlineData("AB123456", "USA")] // 3-letter country (invalid)
    public async Task ValidateDocument_InvalidInput_ReturnsInvalid(string docNumber, string countryCode)
    {
        var request = ValidPassportRequest();
        request.DocumentNumber = docNumber;
        request.CountryCode    = countryCode;

        var result = await CreateService().ValidateDocumentAsync(request);

        result.IsValid.Should().BeFalse();
        result.ValidationErrors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ValidateDocument_BankingApiThrows_AddsManualReviewNote()
    {
        _bankingApi.Setup(x => x.CheckDocumentBlacklistAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BankingApiException("Service down"));

        var result = await CreateService().ValidateDocumentAsync(ValidPassportRequest());

        result.IsValid.Should().BeFalse();
        result.ValidationErrors.Should().Contain(e => e.Contains("unavailable"));
    }

    [Fact]
    public async Task ValidateDocument_AlwaysReturnsReferenceId()
    {
        _bankingApi.Setup(x => x.CheckDocumentBlacklistAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateService().ValidateDocumentAsync(ValidPassportRequest());

        result.ReferenceId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateDocument_DocumentNumberIsMasked()
    {
        _bankingApi.Setup(x => x.CheckDocumentBlacklistAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateService().ValidateDocumentAsync(ValidPassportRequest());

        result.DocumentNumber.Should().NotBe("AB123456");
        result.DocumentNumber.Should().EndWith("3456");
        result.DocumentNumber.Should().Contain("*");
    }
}
