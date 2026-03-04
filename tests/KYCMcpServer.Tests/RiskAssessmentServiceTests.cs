using FluentAssertions;
using KYCMcpServer.Core.Models.Kyc;
using KYCMcpServer.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace KYCMcpServer.Tests;

public class RiskAssessmentServiceTests
{
    private readonly Mock<ILogger<RiskAssessmentService>> _logger = new();

    private RiskAssessmentService CreateService() => new(_logger.Object);

    private static RiskAssessmentRequest LowRiskRequest(string id = "CUST001") => new()
    {
        CustomerId                        = id,
        CustomerType                      = CustomerType.Individual,
        CountryOfResidence                = "US",
        CountryOfOrigin                   = "US",
        SourceOfFunds                     = "Salary",
        ExpectedAnnualTransactionVolume   = 50_000,
        ExpectedMonthlyTransactionCount   = 20
    };

    [Fact]
    public async Task AssessRisk_LowRiskIndividual_ReturnsLow()
    {
        var result = await CreateService().AssessRiskAsync(LowRiskRequest());

        result.RiskLevel.Should().Be(RiskLevel.Low);
        result.RequiresEnhancedDueDiligence.Should().BeFalse();
        result.RequiresManualReview.Should().BeFalse();
    }

    [Fact]
    public async Task AssessRisk_HighRiskCountry_ReturnsHighOrCritical()
    {
        var request = LowRiskRequest();
        request.CountryOfResidence = "IR"; // Iran — FATF listed
        request.CountryOfOrigin    = "IR";

        var result = await CreateService().AssessRiskAsync(request);

        result.RiskLevel.Should().BeOneOf(RiskLevel.High, RiskLevel.Critical);
        result.RequiresEnhancedDueDiligence.Should().BeTrue();
        result.RiskFactors.Should().Contain(f => f.Category == "Geographic");
    }

    [Fact]
    public async Task AssessRisk_HighVolumeCorporate_ScoresHigherThanLowVolumeIndividual()
    {
        var low = LowRiskRequest("LOW");
        var high = new RiskAssessmentRequest
        {
            CustomerId                      = "HIGH",
            CustomerType                    = CustomerType.Corporate,
            CountryOfResidence              = "US",
            CountryOfOrigin                 = "US",
            SourceOfFunds                   = "Investments",
            ExpectedAnnualTransactionVolume = 5_000_000,
            ExpectedMonthlyTransactionCount = 600
        };

        var svc = CreateService();
        var lowResult  = await svc.AssessRiskAsync(low);
        var highResult = await svc.AssessRiskAsync(high);

        highResult.RiskScore.Should().BeGreaterThan(lowResult.RiskScore);
    }

    [Fact]
    public async Task AssessRisk_TrustEntity_HasHigherBaseThanIndividual()
    {
        var individual = LowRiskRequest("IND");
        var trust = new RiskAssessmentRequest
        {
            CustomerId         = "TRUST",
            CustomerType       = CustomerType.Trust,
            CountryOfResidence = "GB",
            CountryOfOrigin    = "GB",
            SourceOfFunds      = "Salary"
        };

        var svc = CreateService();
        var indResult   = await svc.AssessRiskAsync(individual);
        var trustResult = await svc.AssessRiskAsync(trust);

        trustResult.RiskScore.Should().BeGreaterThan(indResult.RiskScore);
        trustResult.RiskFactors.Should().Contain(f => f.Category == "Entity Type");
    }

    [Fact]
    public async Task AssessRisk_MissingSourceOfFunds_AddsRiskFactor()
    {
        var request = LowRiskRequest();
        request.SourceOfFunds = null;

        var result = await CreateService().AssessRiskAsync(request);

        result.RiskFactors.Should().Contain(f => f.Category == "Source of Funds");
    }

    [Fact]
    public async Task AssessRisk_CryptoSourceOfFunds_AddsHighRiskFactor()
    {
        var request = LowRiskRequest();
        request.SourceOfFunds = "Cryptocurrency";

        var result = await CreateService().AssessRiskAsync(request);

        result.RiskFactors.Should().Contain(f =>
            f.Category == "Source of Funds" && f.Severity == "High");
    }

    [Fact]
    public async Task AssessRisk_OutdatedKyc_AddsHistoryFactor()
    {
        var request = LowRiskRequest();
        request.HasPreviousKycRecord = true;
        request.PreviousKycDate      = DateTime.UtcNow.AddDays(-400);

        var result = await CreateService().AssessRiskAsync(request);

        result.RiskFactors.Should().Contain(f => f.Category == "KYC History");
    }

    [Fact]
    public async Task AssessRisk_AlwaysReturnsRecommendedActions()
    {
        var result = await CreateService().AssessRiskAsync(LowRiskRequest());

        result.RecommendedActions.Should().NotBeEmpty();
        result.ReferenceId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AssessRisk_CriticalRisk_RequiresManualReview()
    {
        var request = new RiskAssessmentRequest
        {
            CustomerId                      = "CRIT",
            CustomerType                    = CustomerType.Trust,
            CountryOfResidence              = "KP", // North Korea
            CountryOfOrigin                 = "IR", // Iran
            SourceOfFunds                   = "Cryptocurrency",
            ExpectedAnnualTransactionVolume = 10_000_000
        };

        var result = await CreateService().AssessRiskAsync(request);

        result.RiskLevel.Should().Be(RiskLevel.Critical);
        result.RequiresManualReview.Should().BeTrue();
        result.RequiresEnhancedDueDiligence.Should().BeTrue();
    }
}
