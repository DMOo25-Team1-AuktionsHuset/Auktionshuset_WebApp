using Xunit;
using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Application.Admin.Auctions.CreateAuction;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Service;

namespace Auktionshuset.Tests;

public class CreateAuctionHandlerTests
{
    /// <summary>
    /// Verifies that a valid command stores the auction with its lot lines and quantities.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithSelectedLots_CreatesAuctionWithQuantities()
    {
        Employee employee = TestData.CreateEmployee();
        Lot first = TestData.CreateLot("Stol", quantity: 4);
        Lot second = TestData.CreateLot("Bord", quantity: 2);
        (CreateAuctionHandler? handler, InMemoryAuctionRepository? auctions, TestEmployeeRepository _, RecordingEventPublisher _) = await CreateHandlerAsync(employee, first, second);

        CreateAuctionResult result = await handler.HandleAsync(
            TestData.CreateCommand(
                employee.EmployeeId,
                DateTime.Now.AddDays(7),
                DateTime.Now.AddDays(8),
                new AuctionLotSelection(first.LotId, 2),
                new AuctionLotSelection(second.LotId, 2)),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotEqual(Guid.Empty, result.AuctionId);
        Assert.Equal(2, result.LotCount);
        Assert.Equal(4, result.ItemCount);

        IReadOnlyList<AuctionLot> auctionLots = await auctions.GetAuctionLotsAsync(result.AuctionId, CancellationToken.None);
        Assert.Equal(2, auctionLots.Count);
        Assert.Equal(2, auctionLots.Single(line => line.LotId == first.LotId).Quantity);
        Assert.Equal(2, auctionLots.Single(line => line.LotId == second.LotId).Quantity);
    }

    /// <summary>
    /// Verifies that the stored lot lines keep a reference to the lot, so the dashboard can read its name.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithSelectedLots_KeepsLotReferenceForDashboard()
    {
        Employee employee = TestData.CreateEmployee();
        Lot lot = TestData.CreateLot("Vase");
        (CreateAuctionHandler? handler, InMemoryAuctionRepository? auctions, TestEmployeeRepository _, RecordingEventPublisher _) = await CreateHandlerAsync(employee, lot);

        CreateAuctionResult result = await handler.HandleAsync(
            TestData.CreateCommand(
                employee.EmployeeId,
                DateTime.Now.AddDays(2),
                DateTime.Now.AddDays(3),
                new AuctionLotSelection(lot.LotId, 1)),
            CancellationToken.None);

        AuctionLot storedLotLine = Assert.Single(await auctions.GetAuctionLotsAsync(result.AuctionId, CancellationToken.None));
        Assert.Equal("Vase", storedLotLine.Lot.Name);
    }

    /// <summary>
    /// Verifies that an auction can be created without any genstande.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithoutLots_CreatesAuctionWithNoRelationships()
    {
        Employee employee = TestData.CreateEmployee();
        (CreateAuctionHandler? handler, InMemoryAuctionRepository? auctions, TestEmployeeRepository _, RecordingEventPublisher _) = await CreateHandlerAsync(employee);

        CreateAuctionResult result = await handler.HandleAsync(
            TestData.CreateCommand(employee.EmployeeId, DateTime.Now.AddDays(1), DateTime.Now.AddDays(2)),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.LotCount);
        Assert.Equal(0, result.ItemCount);
        Assert.Empty(await auctions.GetAuctionLotsAsync(result.AuctionId, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that a start time in the past is rejected and nothing is stored.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithPastStart_ReturnsInvalidAndStoresNothing()
    {
        Employee employee = TestData.CreateEmployee();
        (CreateAuctionHandler? handler, InMemoryAuctionRepository? auctions, TestEmployeeRepository _, RecordingEventPublisher _) = await CreateHandlerAsync(employee);

        CreateAuctionResult result = await handler.HandleAsync(
            TestData.CreateCommand(employee.EmployeeId, DateTime.Now.AddDays(-1), DateTime.Now.AddDays(1)),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("fremtiden"));
        Assert.Equal(Guid.Empty, result.AuctionId);
        Assert.Empty(await auctions.GetAllAsync(CancellationToken.None));
    }

    /// <summary>
    /// Verifies that an end time before the start time is rejected.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithEndBeforeStart_ReturnsInvalid()
    {
        Employee employee = TestData.CreateEmployee();
        (CreateAuctionHandler? handler, InMemoryAuctionRepository _, TestEmployeeRepository _, RecordingEventPublisher _) = await CreateHandlerAsync(employee);
        DateTime startsAt = DateTime.Now.AddDays(3);

        CreateAuctionResult result = await handler.HandleAsync(
            TestData.CreateCommand(employee.EmployeeId, startsAt, startsAt.AddHours(-1)),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("efter starttidspunktet"));
    }

    /// <summary>
    /// Verifies that a missing name is rejected.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("a")]
    public async Task HandleAsync_WithInvalidName_ReturnsInvalid(string name)
    {
        Employee employee = TestData.CreateEmployee();
        (CreateAuctionHandler? handler, InMemoryAuctionRepository _, TestEmployeeRepository _, RecordingEventPublisher _) = await CreateHandlerAsync(employee);

        CreateAuctionCommand command = TestData.CreateCommand(employee.EmployeeId, DateTime.Now.AddDays(2), DateTime.Now.AddDays(3)) with
        {
            Name = name
        };

        CreateAuctionResult result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("Auktionsnavn"));
    }

    /// <summary>
    /// Verifies that an unknown auctionarius is rejected.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithUnknownEmployee_ReturnsInvalid()
    {
        (CreateAuctionHandler? handler, InMemoryAuctionRepository _, TestEmployeeRepository _, RecordingEventPublisher _) = await CreateHandlerAsync();

        CreateAuctionResult result = await handler.HandleAsync(
            TestData.CreateCommand(Guid.NewGuid(), DateTime.Now.AddDays(2), DateTime.Now.AddDays(3)),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("auktionarius"));
    }

    /// <summary>
    /// Verifies that an auction can be created without an auctionarius.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithoutEmployee_CreatesAuctionWithoutAuctionarius()
    {
        (CreateAuctionHandler? handler, InMemoryAuctionRepository? auctions, TestEmployeeRepository _, RecordingEventPublisher _) = await CreateHandlerAsync();

        CreateAuctionResult result = await handler.HandleAsync(
            TestData.CreateCommand(null, DateTime.Now.AddDays(2), DateTime.Now.AddDays(3)),
            CancellationToken.None);

        Assert.True(result.Succeeded);

        Auction? auction = await auctions.GetByIdAsync(result.AuctionId, CancellationToken.None);
        Assert.NotNull(auction);
        Assert.Null(auction.EmployeeId);
        Assert.Null(auction.Employee);
    }

    /// <summary>
    /// Verifies that an unknown genstand is rejected.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithUnknownLot_ReturnsInvalid()
    {
        Employee employee = TestData.CreateEmployee();
        (CreateAuctionHandler? handler, InMemoryAuctionRepository _, TestEmployeeRepository _, RecordingEventPublisher _) = await CreateHandlerAsync(employee);

        CreateAuctionResult result = await handler.HandleAsync(
            TestData.CreateCommand(
                employee.EmployeeId,
                DateTime.Now.AddDays(2),
                DateTime.Now.AddDays(3),
                new AuctionLotSelection(Guid.NewGuid(), 1)),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("findes ikke i lageret"));
    }

    /// <summary>
    /// Verifies that the same genstand cannot be added twice.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithDuplicateLots_ReturnsInvalid()
    {
        Employee employee = TestData.CreateEmployee();
        Lot lot = TestData.CreateLot("Lampe", quantity: 3);
        (CreateAuctionHandler? handler, InMemoryAuctionRepository _, TestEmployeeRepository _, RecordingEventPublisher _) = await CreateHandlerAsync(employee, lot);

        CreateAuctionResult result = await handler.HandleAsync(
            TestData.CreateCommand(
                employee.EmployeeId,
                DateTime.Now.AddDays(2),
                DateTime.Now.AddDays(3),
                new AuctionLotSelection(lot.LotId, 1),
                new AuctionLotSelection(lot.LotId, 2)),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("mere end én gang"));
    }

    /// <summary>
    /// Verifies that a quantity below one is rejected.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task HandleAsync_WithQuantityBelowOne_ReturnsInvalid(int quantity)
    {
        Employee employee = TestData.CreateEmployee();
        Lot lot = TestData.CreateLot("Lampe", quantity: 5);
        (CreateAuctionHandler? handler, InMemoryAuctionRepository _, TestEmployeeRepository _, RecordingEventPublisher _) = await CreateHandlerAsync(employee, lot);

        CreateAuctionResult result = await handler.HandleAsync(
            TestData.CreateCommand(
                employee.EmployeeId,
                DateTime.Now.AddDays(2),
                DateTime.Now.AddDays(3),
                new AuctionLotSelection(lot.LotId, quantity)),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("mindst 1"));
    }

    /// <summary>
    /// Verifies that more units than the stock quantity is rejected.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithQuantityAboveStock_ReturnsInvalid()
    {
        Employee employee = TestData.CreateEmployee();
        Lot lot = TestData.CreateLot("Lampe", quantity: 2);
        (CreateAuctionHandler? handler, InMemoryAuctionRepository _, TestEmployeeRepository _, RecordingEventPublisher _) = await CreateHandlerAsync(employee, lot);

        CreateAuctionResult result = await handler.HandleAsync(
            TestData.CreateCommand(
                employee.EmployeeId,
                DateTime.Now.AddDays(2),
                DateTime.Now.AddDays(3),
                new AuctionLotSelection(lot.LotId, 3)),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("kun 2 stk."));
    }

    /// <summary>
    /// Verifies that the status is derived from the time span.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithFutureStart_StoresUpcomingStatus()
    {
        Employee employee = TestData.CreateEmployee();
        (CreateAuctionHandler? handler, InMemoryAuctionRepository? auctions, TestEmployeeRepository _, RecordingEventPublisher _) = await CreateHandlerAsync(employee);

        CreateAuctionResult result = await handler.HandleAsync(
            TestData.CreateCommand(employee.EmployeeId, DateTime.Now.AddDays(5), DateTime.Now.AddDays(6)),
            CancellationToken.None);

        Auction? auction = await auctions.GetByIdAsync(result.AuctionId, CancellationToken.None);
        Assert.NotNull(auction);
        Assert.Equal(AuctionStatuses.Upcoming, auction.AuctionStatus);
    }

    /// <summary>
    /// Verifies that a created auction keeps the chosen auctionarius.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithValidCommand_StoresAuctionarius()
    {
        Employee employee = TestData.CreateEmployee();
        (CreateAuctionHandler? handler, InMemoryAuctionRepository? auctions, TestEmployeeRepository _, RecordingEventPublisher _) = await CreateHandlerAsync(employee);

        CreateAuctionResult result = await handler.HandleAsync(
            TestData.CreateCommand(employee.EmployeeId, DateTime.Now.AddDays(5), DateTime.Now.AddDays(6)),
            CancellationToken.None);

        Auction? auction = await auctions.GetByIdAsync(result.AuctionId, CancellationToken.None);
        Assert.NotNull(auction);
        Assert.Equal(employee.EmployeeId, auction.EmployeeId);
    }

    /// <summary>
    /// Verifies that a created auction publishes an integration event describing it.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithValidCommand_PublishesAuctionCreatedEvent()
    {
        Employee employee = TestData.CreateEmployee();
        Lot lot = TestData.CreateLot("Bord", quantity: 3);
        DateTime startsAt = DateTime.Now.AddDays(5);
        DateTime endsAt = DateTime.Now.AddDays(6);
        (CreateAuctionHandler? handler, InMemoryAuctionRepository _, TestEmployeeRepository _, RecordingEventPublisher? publisher) = await CreateHandlerAsync(employee, lot);

        CreateAuctionResult result = await handler.HandleAsync(
            TestData.CreateCommand(employee.EmployeeId, startsAt, endsAt, new AuctionLotSelection(lot.LotId, 3)),
            CancellationToken.None);

        AuctionCreatedIntegrationEvent published = Assert.Single(publisher.Published.OfType<AuctionCreatedIntegrationEvent>());
        Assert.NotEqual(Guid.Empty, published.EventId);
        Assert.Equal(result.AuctionId, published.AuctionId);
        Assert.Equal("Forårsauktion", published.Name);
        Assert.Equal(startsAt, published.StartsAt);
        Assert.Equal(endsAt, published.EndsAt);
        Assert.Equal(1, published.LotCount);
        Assert.Equal(3, published.ItemCount);
    }

    /// <summary>
    /// Verifies that an invalid command publishes no integration event.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithInvalidCommand_DoesNotPublishEvent()
    {
        Employee employee = TestData.CreateEmployee();
        (CreateAuctionHandler? handler, InMemoryAuctionRepository _, TestEmployeeRepository _, RecordingEventPublisher? publisher) = await CreateHandlerAsync(employee);

        CreateAuctionResult result = await handler.HandleAsync(
            TestData.CreateCommand(employee.EmployeeId, DateTime.Now.AddDays(-2), DateTime.Now.AddDays(1)),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(publisher.Published);
    }

    /// <summary>
    /// Builds a handler with an in-memory auction store, the supplied lots and the seeded employee.
    /// </summary>
    private static async Task<(CreateAuctionHandler Handler, InMemoryAuctionRepository Auctions, TestEmployeeRepository Employees, RecordingEventPublisher Publisher)> CreateHandlerAsync(
        Employee? employee = null,
        params Lot[] lots)
    {
        InMemoryLotRepository lotRepository = await TestData.CreateLotRepositoryAsync(lots);
        var auctionRepository = new InMemoryAuctionRepository();
        var employeeRepository = new TestEmployeeRepository();

        if (employee is not null)
        {
            employeeRepository.Add(employee);
        }

        var publisher = new RecordingEventPublisher();

        return (
            new CreateAuctionHandler(auctionRepository, lotRepository, employeeRepository, publisher),
            auctionRepository,
            employeeRepository,
            publisher);
    }
}
