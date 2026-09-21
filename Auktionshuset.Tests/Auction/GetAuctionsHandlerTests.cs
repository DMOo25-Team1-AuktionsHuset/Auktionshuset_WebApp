using Xunit;
using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Application.Admin.Auctions.CreateAuction;
using Auktionshuset.Application.Admin.Auctions.GetAuctions;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Service;

namespace Auktionshuset.Tests;

public class GetAuctionsHandlerTests
{
    /// <summary>
    /// Verifies that the dashboard rows report both the number of lot lines and the number of units.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithLots_ReportsLotAndItemCounts()
    {
        var employee = TestData.CreateEmployee();
        var first = TestData.CreateLot("Stol", quantity: 5);
        var second = TestData.CreateLot("Bord", quantity: 4);
        var (handler, auctionId) = await CreateAsync(employee, (first, 2), (second, 1));

        var overviews = await handler.HandleAsync(CancellationToken.None);

        var overview = Assert.Single(overviews);
        Assert.Equal(auctionId, overview.Auction.AuctionId);
        Assert.Equal(2, overview.LotCount);
        Assert.Equal(3, overview.ItemCount);
    }

    /// <summary>
    /// Verifies that the auctionarius name is resolved for the dashboard.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithAuctionarius_ReportsEmployeeName()
    {
        var employee = TestData.CreateEmployee("Henrik", "Sørensen");
        var (handler, _) = await CreateAsync(employee);

        var overviews = await handler.HandleAsync(CancellationToken.None);

        Assert.Equal("Henrik Sørensen", Assert.Single(overviews).EmployeeName);
    }

    /// <summary>
    /// Verifies that an auctionarius that is no longer stored is reported as not specified.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithoutStoredEmployee_ReportsFallbackName()
    {
        var employee = TestData.CreateEmployee();
        var auctionRepository = new InMemoryAuctionRepository();

        var auction = new Auction
        {
            AuctionId = Guid.NewGuid(),
            Name = "Forårsauktion",
            StartsAt = DateTime.Now.AddDays(2),
            EndsAt = DateTime.Now.AddDays(3),
            EmployeeId = Guid.NewGuid(),
            AuctionHouseId = TestData.AuctionHouseId,
            AuctionStatus = AuctionStatuses.Upcoming
        };

        await auctionRepository.AddAsync(auction, [], CancellationToken.None);

        var handler = new GetAuctionsHandler(auctionRepository, new TestEmployeeRepository());

        var overviews = await handler.HandleAsync(CancellationToken.None);

        Assert.Equal("Ikke angivet", Assert.Single(overviews).EmployeeName);
        Assert.NotEqual(Guid.Empty, employee.EmployeeId);
    }

    /// <summary>
    /// Verifies that the image file names of the lots are reported so thumbnails can be built.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithLotImages_ReportsImageFileNames()
    {
        var employee = TestData.CreateEmployee();
        var lot = TestData.CreateLot("Vase", imageFileName: "a1b2c3.png");
        var (handler, _) = await CreateAsync(employee, (lot, 1));

        var overviews = await handler.HandleAsync(CancellationToken.None);

        Assert.Equal("a1b2c3.png", Assert.Single(Assert.Single(overviews).ImageFileNames));
    }

    /// <summary>
    /// Verifies that the lot lines of an auction are reported in the detail view.
    /// </summary>
    [Fact]
    public async Task HandleByIdAsync_WithLots_ReturnsLotLines()
    {
        var employee = TestData.CreateEmployee();
        var lot = TestData.CreateLot("Vase", quantity: 3, imageFileName: "bilde.webp");
        var (handler, auctionId) = await CreateAsync(employee, (lot, 1));

        var detail = await handler.HandleByIdAsync(auctionId, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(1, detail.LotCount);
        Assert.Equal(1, detail.ItemCount);

        var line = Assert.Single(detail.Lots);
        Assert.Equal(lot.LotId, line.LotId);
        Assert.Equal("Vase", line.Name);
        Assert.Equal("Møbler", line.Category);
        Assert.Equal(1, line.Quantity);
        Assert.Equal("bilde.webp", line.ImageFileName);
    }

    /// <summary>
    /// Verifies that a detail lookup for an unknown auction returns nothing.
    /// </summary>
    [Fact]
    public async Task HandleByIdAsync_WithUnknownAuction_ReturnsNull()
    {
        var employee = TestData.CreateEmployee();
        var (handler, _) = await CreateAsync(employee);

        Assert.Null(await handler.HandleByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    /// <summary>
    /// Verifies that the dashboard lists every auction with the newest start time first.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithSeveralAuctions_OrdersByStartTimeDescending()
    {
        var employee = TestData.CreateEmployee();
        var lotRepository = await TestData.CreateLotRepositoryAsync();
        var auctionRepository = new InMemoryAuctionRepository();
        var employeeRepository = new TestEmployeeRepository();
        employeeRepository.Add(employee);
        var publisher = new RecordingEventPublisher();
        var createHandler = new CreateAuctionHandler(auctionRepository, lotRepository, employeeRepository, publisher);

        var earliest = await createHandler.HandleAsync(
            TestData.CreateCommand(employee.EmployeeId, DateTime.Now.AddDays(1), DateTime.Now.AddDays(2)) with { Name = "Tidlig" },
            CancellationToken.None);

        var latest = await createHandler.HandleAsync(
            TestData.CreateCommand(employee.EmployeeId, DateTime.Now.AddDays(20), DateTime.Now.AddDays(21)) with { Name = "Sen" },
            CancellationToken.None);

        var handler = new GetAuctionsHandler(auctionRepository, employeeRepository);

        var overviews = await handler.HandleAsync(CancellationToken.None);

        Assert.Equal(2, overviews.Count);
        Assert.Equal(latest.AuctionId, overviews[0].Auction.AuctionId);
        Assert.Equal(earliest.AuctionId, overviews[1].Auction.AuctionId);
    }

    /// <summary>
    /// Creates one auction with the supplied genstande and quantities and returns a dashboard handler.
    /// </summary>
    private static async Task<(GetAuctionsHandler Handler, Guid AuctionId)> CreateAsync(
        Employee employee,
        params (Lot Lot, int Quantity)[] selections)
    {
        var lots = selections.Select(selection => selection.Lot).ToArray();
        var lotRepository = await TestData.CreateLotRepositoryAsync(lots);
        var auctionRepository = new InMemoryAuctionRepository();
        var employeeRepository = new TestEmployeeRepository();
        employeeRepository.Add(employee);
        var publisher = new RecordingEventPublisher();

        var created = await new CreateAuctionHandler(auctionRepository, lotRepository, employeeRepository, publisher)
            .HandleAsync(
                TestData.CreateCommand(
                    employee.EmployeeId,
                    DateTime.Now.AddDays(5),
                    DateTime.Now.AddDays(6),
                    [.. selections.Select(selection => new AuctionLotSelection(selection.Lot.LotId, selection.Quantity))]),
                CancellationToken.None);

        Assert.True(created.Succeeded);

        return (new GetAuctionsHandler(auctionRepository, employeeRepository), created.AuctionId);
    }
}
