using System.ComponentModel.DataAnnotations;
using Auktionshuset.Contracts.Dto.Admin.Auction;
using Xunit;

namespace Auktionshuset.Tests;

public class CreateAuctionRequestTests
{
    private static List<ValidationResult> Validate(CreateAuctionRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Validate_WithFutureDateAndNoLots_HasNoErrors()
    {
        var request = new CreateAuctionRequest { StartsAt = DateTime.Now.AddDays(3), LotIds = [] };

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void Validate_WithoutDate_ReturnsError()
    {
        var request = new CreateAuctionRequest { StartsAt = null, LotIds = [] };

        var errors = Validate(request);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(CreateAuctionRequest.StartsAt)));
    }

    [Fact]
    public void Validate_WithPastDate_ReturnsError()
    {
        var request = new CreateAuctionRequest { StartsAt = DateTime.Now.AddDays(-1), LotIds = [] };

        var errors = Validate(request);

        Assert.Contains(errors, error => error.ErrorMessage?.Contains("fremtiden") == true);
    }

    [Fact]
    public void Validate_WithDuplicateLotIds_ReturnsError()
    {
        var lotId = Guid.NewGuid();
        var request = new CreateAuctionRequest
        {
            StartsAt = DateTime.Now.AddDays(3),
            LotIds = [lotId, lotId]
        };

        var errors = Validate(request);

        Assert.Contains(errors, error => error.ErrorMessage?.Contains("mere end én gang") == true);
    }
}
