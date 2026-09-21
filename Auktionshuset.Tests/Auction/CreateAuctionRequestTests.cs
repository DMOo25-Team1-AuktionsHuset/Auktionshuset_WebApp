using Xunit;
using System.ComponentModel.DataAnnotations;
using Auktionshuset.Contracts.Dto.Admin.Auction;

namespace Auktionshuset.Tests;

public class CreateAuctionRequestTests
{
    /// <summary>
    /// Verifies that a complete request passes validation.
    /// </summary>
    [Fact]
    public void Validate_WithValidValues_HasNoErrors()
    {
        Assert.Empty(Validate(CreateValidRequest()));
    }

    /// <summary>
    /// Verifies that the auction name is required.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("a")]
    public void Validate_WithInvalidName_ReturnsError(string name)
    {
        var errors = Validate(CreateValidRequest(name: name));

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(CreateAuctionRequest.Name)));
    }

    /// <summary>
    /// Verifies that the start time is required.
    /// </summary>
    [Fact]
    public void Validate_WithoutStart_ReturnsError()
    {
        var request = new CreateAuctionRequest
        {
            Name = "Forårsauktion",
            EndsAt = DateTime.Now.AddDays(4),
            EmployeeId = Guid.NewGuid()
        };

        var errors = Validate(request);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(CreateAuctionRequest.StartsAt)));
    }

    /// <summary>
    /// Verifies that the start time has to lie in the future when an auction is created.
    /// </summary>
    [Fact]
    public void Validate_WithPastStart_ReturnsError()
    {
        var errors = Validate(CreateValidRequest(startsAt: DateTime.Now.AddDays(-1)));

        Assert.Contains(errors, error => error.ErrorMessage?.Contains("fremtiden") == true);
    }

    /// <summary>
    /// Verifies that the end time has to lie after the start time.
    /// </summary>
    [Fact]
    public void Validate_WithEndBeforeStart_ReturnsError()
    {
        var startsAt = DateTime.Now.AddDays(3);

        var errors = Validate(CreateValidRequest(startsAt: startsAt, endsAt: startsAt.AddHours(-2)));

        Assert.Contains(errors, error => error.ErrorMessage?.Contains("efter starttidspunktet") == true);
    }

    /// <summary>
    /// Verifies that an auctionarius is required.
    /// </summary>
    [Fact]
    public void Validate_WithoutEmployee_ReturnsError()
    {
        var request = new CreateAuctionRequest
        {
            Name = "Forårsauktion",
            StartsAt = DateTime.Now.AddDays(3),
            EndsAt = DateTime.Now.AddDays(4)
        };

        var errors = Validate(request);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(CreateAuctionRequest.EmployeeId)));
    }

    /// <summary>
    /// Verifies that the same genstand cannot appear twice.
    /// </summary>
    [Fact]
    public void Validate_WithDuplicateLots_ReturnsError()
    {
        var lotId = Guid.NewGuid();

        var errors = Validate(CreateValidRequest(lots:
            [new AuctionLotRequest(lotId, 1), new AuctionLotRequest(lotId, 2)]));

        Assert.Contains(errors, error => error.ErrorMessage?.Contains("mere end én gang") == true);
    }

    /// <summary>
    /// Verifies that every chosen quantity has to be at least one.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_WithQuantityBelowOne_ReturnsError(int quantity)
    {
        var errors = Validate(CreateValidRequest(lots: [new AuctionLotRequest(Guid.NewGuid(), quantity)]));

        Assert.Contains(errors, error => error.ErrorMessage?.Contains("mindst 1") == true);
    }

    /// <summary>
    /// Builds a request that satisfies every validation rule, allowing individual fields to be overridden.
    /// </summary>
    private static CreateAuctionRequest CreateValidRequest(
        string name = "Forårsauktion",
        DateTime? startsAt = null,
        DateTime? endsAt = null,
        AuctionLotRequest[]? lots = null) => new()
    {
        Name = name,
        StartsAt = startsAt ?? DateTime.Now.AddDays(3),
        EndsAt = endsAt ?? DateTime.Now.AddDays(4),
        EmployeeId = Guid.NewGuid(),
        AuctionHouseId = TestData.AuctionHouseId,
        Lots = lots ?? [new AuctionLotRequest(Guid.NewGuid(), 2)]
    };

    /// <summary>
    /// Runs annotation and <see cref="IValidatableObject"/> validation on the request.
    /// </summary>
    private static IReadOnlyList<ValidationResult> Validate(CreateAuctionRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }
}
