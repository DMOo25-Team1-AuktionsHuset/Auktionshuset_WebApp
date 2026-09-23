using Xunit;
using System.ComponentModel.DataAnnotations;
using Auktionshuset.Contracts.Dto.Admin.Auction;

namespace Auktionshuset.Tests;

public class UpdateAuctionRequestTests
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
    /// Verifies that an ongoing auction can be updated with a start time in the past, since only the
    /// relative order of the times has to hold when editing.
    /// </summary>
    [Fact]
    public void Validate_WithPastStart_IsAllowedForUpdate()
    {
        Assert.Empty(Validate(CreateValidRequest(
            startsAt: DateTime.Now.AddDays(-1),
            endsAt: DateTime.Now.AddDays(1))));
    }

    /// <summary>
    /// Verifies that the end time has to lie after the start time.
    /// </summary>
    [Fact]
    public void Validate_WithEndBeforeStart_ReturnsError()
    {
        var startsAt = DateTime.Now.AddDays(4);

        var errors = Validate(CreateValidRequest(startsAt: startsAt, endsAt: startsAt.AddMinutes(-1)));

        Assert.Contains(errors, error => error.ErrorMessage?.Contains("efter starttidspunktet") == true);
    }

    /// <summary>
    /// Verifies that the name is still required when updating.
    /// </summary>
    [Fact]
    public void Validate_WithoutName_ReturnsError()
    {
        var errors = Validate(CreateValidRequest(name: string.Empty));

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(UpdateAuctionRequest.Name)));
    }

    /// <summary>
    /// Verifies that an auctionarius is required when updating.
    /// </summary>
    [Fact]
    public void Validate_WithoutEmployee_ReturnsError()
    {
        var request = new UpdateAuctionRequest
        {
            Name = "Forårsauktion",
            StartsAt = DateTime.Now.AddDays(3),
            EndsAt = DateTime.Now.AddDays(4)
        };

        Assert.Contains(
            Validate(request),
            error => error.MemberNames.Contains(nameof(UpdateAuctionRequest.EmployeeId)));
    }

    /// <summary>
    /// Verifies that duplicated genstande and invalid quantities are both rejected.
    /// </summary>
    [Fact]
    public void Validate_WithDuplicateLotsAndLowQuantity_ReturnsBothErrors()
    {
        var lotId = Guid.NewGuid();

        var errors = Validate(CreateValidRequest(lots:
            [new AuctionLotRequest(lotId, 0), new AuctionLotRequest(lotId, 1)]));

        Assert.Contains(errors, error => error.ErrorMessage?.Contains("mere end én gang") == true);
        Assert.Contains(errors, error => error.ErrorMessage?.Contains("mindst 1") == true);
    }

    /// <summary>
    /// Builds a request that satisfies every validation rule, allowing individual fields to be overridden.
    /// </summary>
    private static UpdateAuctionRequest CreateValidRequest(
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
        Lots = lots ?? [new AuctionLotRequest(Guid.NewGuid(), 1)]
    };

    /// <summary>
    /// Runs annotation and <see cref="IValidatableObject"/> validation on the request.
    /// </summary>
    private static IReadOnlyList<ValidationResult> Validate(UpdateAuctionRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }
}
