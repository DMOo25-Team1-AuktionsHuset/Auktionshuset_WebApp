using Xunit;
using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Application.Admin.Auctions.CreateAuction;
using Auktionshuset.Application.Admin.Auctions.UpdateAuction;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Service;

namespace Auktionshuset.Tests;

public class UpdateAuctionHandlerTests
{
    /// <summary>
    /// Verifies that updating replaces the auction values and its lot lines with quantities.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithValidCommand_ReplacesValuesAndLotLines()
    {
        var employee = TestData.CreateEmployee();
        var first = TestData.CreateLot("Stol", quantity: 4);
        var second = TestData.CreateLot("Bord", quantity: 6);
        var (handler, auctions, _, _, auctionId) = await CreateExistingAuctionAsync(employee, first, second);

        var result = await handler.HandleAsync(
            new UpdateAuctionCommand(
                AuctionId: auctionId,
                Name: "Efterårsauktion",
                StartsAt: DateTime.Now.AddDays(10),
                EndsAt: DateTime.Now.AddDays(11),
                EmployeeId: employee.EmployeeId,
                AuctionHouseId: null,
                Lots: [new AuctionLotSelection(second.LotId, 5)]),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.LotCount);
        Assert.Equal(5, result.ItemCount);

        var auction = await auctions.GetByIdAsync(auctionId, CancellationToken.None);
        Assert.NotNull(auction);
        Assert.Equal("Efterårsauktion", auction.Name);

        var auctionLots = await auctions.GetAuctionLotsAsync(auctionId, CancellationToken.None);
        var line = Assert.Single(auctionLots);
        Assert.Equal(second.LotId, line.LotId);
        Assert.Equal(5, line.Quantity);
    }

    /// <summary>
    /// Verifies that editing an auction that already started is not blocked by the future-start rule.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ForAuctionThatAlreadyStarted_IsAllowed()
    {
        var employee = TestData.CreateEmployee();
        var (handler, _, _, _, auctionId) = await CreateStartedAuctionAsync(employee);

        var result = await handler.HandleAsync(
            new UpdateAuctionCommand(
                AuctionId: auctionId,
                Name: "Igangværende auktion",
                StartsAt: DateTime.Now.AddDays(-2),
                EndsAt: DateTime.Now.AddDays(3),
                EmployeeId: employee.EmployeeId,
                AuctionHouseId: null,
                Lots: []),
            CancellationToken.None);

        Assert.True(result.Succeeded);
    }

    /// <summary>
    /// Verifies that updating an auction that no longer exists reports a missing auction.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithUnknownAuction_ReportsNotFound()
    {
        var employee = TestData.CreateEmployee();
        var (handler, _, _, _, _) = await CreateExistingAuctionAsync(employee);

        var result = await handler.HandleAsync(
            new UpdateAuctionCommand(
                AuctionId: Guid.NewGuid(),
                Name: "Ukendt",
                StartsAt: DateTime.Now.AddDays(1),
                EndsAt: DateTime.Now.AddDays(2),
                EmployeeId: employee.EmployeeId,
                AuctionHouseId: null,
                Lots: []),
            CancellationToken.None);

        Assert.True(result.NotFound);
        Assert.False(result.Succeeded);
    }

    /// <summary>
    /// Verifies that an invalid time span is rejected.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithEndBeforeStart_ReturnsInvalid()
    {
        var employee = TestData.CreateEmployee();
        var (handler, _, _, _, auctionId) = await CreateExistingAuctionAsync(employee);
        var startsAt = DateTime.Now.AddDays(4);

        var result = await handler.HandleAsync(
            new UpdateAuctionCommand(
                AuctionId: auctionId,
                Name: "Forårsauktion",
                StartsAt: startsAt,
                EndsAt: startsAt.AddMinutes(-1),
                EmployeeId: employee.EmployeeId,
                AuctionHouseId: null,
                Lots: []),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("efter starttidspunktet"));
    }

    /// <summary>
    /// Verifies that an unknown auctionarius is rejected.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithUnknownEmployee_ReturnsInvalid()
    {
        var employee = TestData.CreateEmployee();
        var (handler, _, _, _, auctionId) = await CreateExistingAuctionAsync(employee);

        var result = await handler.HandleAsync(
            new UpdateAuctionCommand(
                AuctionId: auctionId,
                Name: "Forårsauktion",
                StartsAt: DateTime.Now.AddDays(3),
                EndsAt: DateTime.Now.AddDays(4),
                EmployeeId: Guid.NewGuid(),
                AuctionHouseId: null,
                Lots: []),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("auktionarius"));
    }

    /// <summary>
    /// Verifies that an existing auctionarius can be removed again by updating without one.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithoutEmployee_RemovesAuctionarius()
    {
        var employee = TestData.CreateEmployee();
        var (handler, auctions, _, _, auctionId) = await CreateExistingAuctionAsync(employee);

        var result = await handler.HandleAsync(
            new UpdateAuctionCommand(
                AuctionId: auctionId,
                Name: "Forårsauktion",
                StartsAt: DateTime.Now.AddDays(3),
                EndsAt: DateTime.Now.AddDays(4),
                EmployeeId: null,
                AuctionHouseId: null,
                Lots: []),
            CancellationToken.None);

        Assert.True(result.Succeeded);

        var auction = await auctions.GetByIdAsync(auctionId, CancellationToken.None);
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
        var employee = TestData.CreateEmployee();
        var (handler, _, _, _, auctionId) = await CreateExistingAuctionAsync(employee);

        var result = await handler.HandleAsync(
            new UpdateAuctionCommand(
                AuctionId: auctionId,
                Name: "Forårsauktion",
                StartsAt: DateTime.Now.AddDays(3),
                EndsAt: DateTime.Now.AddDays(4),
                EmployeeId: employee.EmployeeId,
                AuctionHouseId: null,
                Lots: [new AuctionLotSelection(Guid.NewGuid(), 1)]),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("findes ikke i lageret"));
    }

    /// <summary>
    /// Verifies that a duplicated genstand is rejected.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithDuplicateLots_ReturnsInvalid()
    {
        var employee = TestData.CreateEmployee();
        var lot = TestData.CreateLot("Lampe", quantity: 4);
        var (handler, _, _, _, auctionId) = await CreateExistingAuctionAsync(employee, lot);

        var result = await handler.HandleAsync(
            new UpdateAuctionCommand(
                AuctionId: auctionId,
                Name: "Forårsauktion",
                StartsAt: DateTime.Now.AddDays(3),
                EndsAt: DateTime.Now.AddDays(4),
                EmployeeId: employee.EmployeeId,
                AuctionHouseId: null,
                Lots: [new AuctionLotSelection(lot.LotId, 1), new AuctionLotSelection(lot.LotId, 2)]),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("mere end én gang"));
    }

    /// <summary>
    /// Verifies that an invalid quantity is rejected.
    /// </summary>
    [Theory]
    [InlineData(0, "mindst 1")]
    [InlineData(9, "kun 4 stk.")]
    public async Task HandleAsync_WithInvalidQuantity_ReturnsInvalid(int quantity, string expectedMessage)
    {
        var employee = TestData.CreateEmployee();
        var lot = TestData.CreateLot("Lampe", quantity: 4);
        var (handler, _, _, _, auctionId) = await CreateExistingAuctionAsync(employee, lot);

        var result = await handler.HandleAsync(
            new UpdateAuctionCommand(
                AuctionId: auctionId,
                Name: "Forårsauktion",
                StartsAt: DateTime.Now.AddDays(3),
                EndsAt: DateTime.Now.AddDays(4),
                EmployeeId: employee.EmployeeId,
                AuctionHouseId: null,
                Lots: [new AuctionLotSelection(lot.LotId, quantity)]),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains(expectedMessage));
    }

    /// <summary>
    /// Verifies that a successful update publishes an integration event describing the change.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithValidCommand_PublishesAuctionUpdatedEvent()
    {
        var employee = TestData.CreateEmployee();
        var (handler, _, _, publisher, auctionId) = await CreateExistingAuctionAsync(employee);

        var result = await handler.HandleAsync(
            new UpdateAuctionCommand(
                AuctionId: auctionId,
                Name: "Vinterauktion",
                StartsAt: DateTime.Now.AddDays(6),
                EndsAt: DateTime.Now.AddDays(7),
                EmployeeId: employee.EmployeeId,
                AuctionHouseId: null,
                Lots: []),
            CancellationToken.None);

        var published = Assert.Single(publisher.Published.OfType<AuctionUpdatedIntegrationEvent>());
        Assert.Equal(result.AuctionId, published.AuctionId);
        Assert.Equal("Vinterauktion", published.Name);
        Assert.Equal(AuctionStatuses.Upcoming, published.Status);
    }

    /// <summary>
    /// Verifies that an invalid update publishes no integration event.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithInvalidCommand_DoesNotPublishEvent()
    {
        var employee = TestData.CreateEmployee();
        var (handler, _, _, publisher, auctionId) = await CreateExistingAuctionAsync(employee);

        var result = await handler.HandleAsync(
            new UpdateAuctionCommand(
                AuctionId: auctionId,
                Name: "Forårsauktion",
                StartsAt: DateTime.Now.AddDays(4),
                EndsAt: DateTime.Now.AddDays(-4),
                EmployeeId: employee.EmployeeId,
                AuctionHouseId: null,
                Lots: []),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(publisher.Published.OfType<AuctionUpdatedIntegrationEvent>());
    }

    /// <summary>
    /// Creates an auction through the create handler and returns an update handler for it.
    /// </summary>
    private static async Task<(UpdateAuctionHandler Handler, InMemoryAuctionRepository Auctions, TestEmployeeRepository Employees, RecordingEventPublisher Publisher, Guid AuctionId)> CreateExistingAuctionAsync(
        Employee employee,
        params Lot[] lots)
    {
        var lotRepository = await TestData.CreateLotRepositoryAsync(lots);
        InMemoryAuctionRepository auctionRepository = new InMemoryAuctionRepository();
        TestEmployeeRepository employeeRepository = new TestEmployeeRepository();
        employeeRepository.Add(employee);

        RecordingEventPublisher publisher = new RecordingEventPublisher();

        var created = await new CreateAuctionHandler(auctionRepository, lotRepository, employeeRepository, publisher)
            .HandleAsync(
                TestData.CreateCommand(
                    employee.EmployeeId,
                    DateTime.Now.AddDays(5),
                    DateTime.Now.AddDays(6),
                    [.. lots.Select(lot => new AuctionLotSelection(lot.LotId, 1))]),
                CancellationToken.None);

        Assert.True(created.Succeeded);

        return (
            new UpdateAuctionHandler(auctionRepository, lotRepository, employeeRepository, publisher),
            auctionRepository,
            employeeRepository,
            publisher,
            created.AuctionId);
    }

    /// <summary>
    /// Stores an auction that is already running, since the create handler refuses a start time in
    /// the past.
    /// </summary>
    private static async Task<(UpdateAuctionHandler Handler, InMemoryAuctionRepository Auctions, TestEmployeeRepository Employees, RecordingEventPublisher Publisher, Guid AuctionId)> CreateStartedAuctionAsync(
        Employee employee)
    {
        var lotRepository = await TestData.CreateLotRepositoryAsync();
        InMemoryAuctionRepository auctionRepository = new InMemoryAuctionRepository();
        TestEmployeeRepository employeeRepository = new TestEmployeeRepository();
        employeeRepository.Add(employee);
        RecordingEventPublisher publisher = new RecordingEventPublisher();

        Auction auction = new Auction
        {
            AuctionId = Guid.NewGuid(),
            Name = "Igangværende auktion",
            StartsAt = DateTime.Now.AddDays(-2),
            EndsAt = DateTime.Now.AddDays(2),
            EmployeeId = employee.EmployeeId,
            Employee = employee,
            AuctionHouseId = TestData.AuctionHouseId,
            AuctionStatus = AuctionStatuses.Live
        };

        await auctionRepository.AddAsync(auction, [], CancellationToken.None);

        return (
            new UpdateAuctionHandler(auctionRepository, lotRepository, employeeRepository, publisher),
            auctionRepository,
            employeeRepository,
            publisher,
            auction.AuctionId);
    }
}
