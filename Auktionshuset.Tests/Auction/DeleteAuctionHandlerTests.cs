using Xunit;
using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Application.Admin.Auctions.CreateAuction;
using Auktionshuset.Application.Admin.Auctions.DeleteAuction;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Service;

namespace Auktionshuset.Tests;

public class DeleteAuctionHandlerTests
{
    /// <summary>
    /// Verifies that deleting removes the auction together with its lot lines.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithExistingAuction_RemovesAuctionAndLotLines()
    {
        Employee employee = TestData.CreateEmployee();
        Lot lot = TestData.CreateLot("Stol");
        (DeleteAuctionHandler? handler, InMemoryAuctionRepository? auctions, Guid auctionId) = await CreateHandlerAsync(employee, lot);

        bool deleted = await handler.HandleAsync(new DeleteAuctionCommand(auctionId), CancellationToken.None);

        Assert.True(deleted);
        Assert.Null(await auctions.GetByIdAsync(auctionId, CancellationToken.None));
        Assert.Empty(await auctions.GetAuctionLotsAsync(auctionId, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that deleting removes only the requested auction.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithTwoAuctions_RemovesOnlyRequested()
    {
        Employee employee = TestData.CreateEmployee();
        (DeleteAuctionHandler? handler, InMemoryAuctionRepository? auctions, Guid first, Guid second) = await CreateTwoAuctionsAsync(employee);

        bool deleted = await handler.HandleAsync(new DeleteAuctionCommand(first), CancellationToken.None);

        Assert.True(deleted);
        Assert.Null(await auctions.GetByIdAsync(first, CancellationToken.None));
        Assert.NotNull(await auctions.GetByIdAsync(second, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that deleting an auction that does not exist reports failure and publishes nothing.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithUnknownAuction_ReturnsFalseAndPublishesNothing()
    {
        Employee employee = TestData.CreateEmployee();
        (DeleteAuctionHandler? handler, InMemoryAuctionRepository _, Guid _) = await CreateHandlerAsync(employee);

        bool deleted = await handler.HandleAsync(new DeleteAuctionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.False(deleted);
    }

    /// <summary>
    /// Verifies that a deletion publishes an integration event for the removed auction.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithExistingAuction_PublishesAuctionDeletedEvent()
    {
        Employee employee = TestData.CreateEmployee();
        (DeleteAuctionHandler? handler, InMemoryAuctionRepository _, Guid auctionId, RecordingEventPublisher? publisher) = await CreateHandlerWithPublisherAsync(employee);

        await handler.HandleAsync(new DeleteAuctionCommand(auctionId), CancellationToken.None);

        AuctionDeletedIntegrationEvent published = Assert.Single(publisher.Published.OfType<AuctionDeletedIntegrationEvent>());
        Assert.Equal(auctionId, published.AuctionId);
        Assert.NotEqual(Guid.Empty, published.EventId);
    }

    private static async Task<(DeleteAuctionHandler Handler, InMemoryAuctionRepository Auctions, Guid AuctionId)> CreateHandlerAsync(
        Employee employee,
        params Lot[] lots)
    {
        (DeleteAuctionHandler? handler, InMemoryAuctionRepository? auctions, Guid auctionId, RecordingEventPublisher _) = await CreateHandlerWithPublisherAsync(employee, lots);
        return (handler, auctions, auctionId);
    }

    private static async Task<(DeleteAuctionHandler Handler, InMemoryAuctionRepository Auctions, Guid First, Guid Second)> CreateTwoAuctionsAsync(
        Employee employee)
    {
        InMemoryLotRepository lots = await TestData.CreateLotRepositoryAsync();
        var auctions = new InMemoryAuctionRepository();
        var employees = new TestEmployeeRepository();
        employees.Add(employee);
        var publisher = new RecordingEventPublisher();

        var createHandler = new CreateAuctionHandler(auctions, lots, employees, publisher);

        CreateAuctionResult first = await createHandler.HandleAsync(
            TestData.CreateCommand(employee.EmployeeId, DateTime.Now.AddDays(1), DateTime.Now.AddDays(2)),
            CancellationToken.None);

        CreateAuctionResult second = await createHandler.HandleAsync(
            TestData.CreateCommand(employee.EmployeeId, DateTime.Now.AddDays(3), DateTime.Now.AddDays(4)),
            CancellationToken.None);

        return (new DeleteAuctionHandler(auctions, publisher), auctions, first.AuctionId, second.AuctionId);
    }

    private static async Task<(DeleteAuctionHandler Handler, InMemoryAuctionRepository Auctions, Guid AuctionId, RecordingEventPublisher Publisher)> CreateHandlerWithPublisherAsync(
        Employee employee,
        params Lot[] lots)
    {
        InMemoryLotRepository lotRepository = await TestData.CreateLotRepositoryAsync(lots);
        var auctionRepository = new InMemoryAuctionRepository();
        var employeeRepository = new TestEmployeeRepository();
        employeeRepository.Add(employee);

        var publisher = new RecordingEventPublisher();

        CreateAuctionResult created = await new CreateAuctionHandler(auctionRepository, lotRepository, employeeRepository, publisher)
            .HandleAsync(
                TestData.CreateCommand(
                    employee.EmployeeId,
                    DateTime.Now.AddDays(5),
                    DateTime.Now.AddDays(6),
                    [.. lots.Select(lot => new AuctionLotSelection(lot.LotId, 1))]),
                CancellationToken.None);

        Assert.True(created.Succeeded);

        return (new DeleteAuctionHandler(auctionRepository, publisher), auctionRepository, created.AuctionId, publisher);
    }
}
