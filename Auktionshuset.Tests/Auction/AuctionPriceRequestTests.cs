using System.ComponentModel.DataAnnotations;
using Auktionshuset.Contracts.Dto.Admin.Auction;
using Xunit;

namespace Auktionshuset.Tests.Auctions;

public class AuctionPriceRequestTests
{
    [Theory]
    [InlineData("0", true)]
    [InlineData("12.34", true)]
    [InlineData("9999999999999999.99", true)]
    [InlineData("-0.01", false)]
    [InlineData("0.001", false)]
    [InlineData("10000000000000000", false)]
    public void BothContractsValidateLinePrices(string value, bool valid)
    {
        var price = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        var lots = new[] { new AuctionLotRequest(Guid.NewGuid(), 3, price) };
        var create = new CreateAuctionRequest { Name = "Testauktion", StartsAt = DateTimeOffset.UtcNow.AddDays(1), Lots = lots };
        var update = new UpdateAuctionRequest { Name = create.Name, StartsAt = create.StartsAt, Lots = lots };
        foreach (var request in new object[] { create, update })
            Assert.Equal(valid, Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }
}
