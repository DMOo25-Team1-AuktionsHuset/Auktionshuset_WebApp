using Auktionshuset.Api.Endpoints.Admin.Lot.DeleteLot;
using Auktionshuset.Application.Admin.Lots.DeleteLot;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Service;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Auktionshuset.Api.Test.Admins.Lots;

public class DeleteLotTest
{
    /// <summary>
    /// Verifies that deleting removes only the requested lot and that deleting it again returns 404.
    /// </summary>
    [Fact]
    public async Task Delete_RemovesOnlyRequestedLot_AndReturns404OnRepeat()
    {
        var repository = new InMemoryLotRepository();
        var target = CreateLot();
        var other = CreateLot();
        await repository.AddAsync(target, CancellationToken.None);
        await repository.AddAsync(other, CancellationToken.None);
        var handler = new DeleteLotHandler(repository);

        var result = await DeleteLotEndpoint.HandleAsync(target.LotId, handler, CancellationToken.None);

        Assert.IsType<NoContent>(result.Result);
        var remaining = await repository.GetAllAsync(CancellationToken.None);
        Assert.Equal(other.LotId, Assert.Single(remaining).LotId);
        var repeated = await DeleteLotEndpoint.HandleAsync(target.LotId, handler, CancellationToken.None);
        Assert.IsType<NotFound>(repeated.Result);
    }

    /// <summary>
    /// Verifies that a cancelled request throws and leaves the lot untouched.
    /// </summary>
    [Fact]
    public async Task Delete_WhenCancelled_DoesNotRemoveLot()
    {
        var repository = new InMemoryLotRepository();
        var lot = CreateLot();
        await repository.AddAsync(lot, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DeleteLotEndpoint.HandleAsync(lot.LotId, new DeleteLotHandler(repository), cancellation.Token));

        Assert.Equal(lot.LotId, Assert.Single(await repository.GetAllAsync(CancellationToken.None)).LotId);
    }

    /// <summary>
    /// Builds a valid lot for use in the tests.
    /// </summary>
    /// <returns>A lot with valid values and a newly generated identifier.</returns>
    private static Lot CreateLot() => new()
    {
        LotId = Guid.NewGuid(), AuctionHouseId = Guid.NewGuid(),
        Name = "Vase", Category = "Ceramics", Quantity = 1,
        EstimatedValue = 100, Description = "Antique vase", Tags = []
    };
}
