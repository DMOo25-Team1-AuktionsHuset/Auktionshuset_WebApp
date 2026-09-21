using Auktionshuset.Api.Endpoints.Admin.CreateAuction;
using Auktionshuset.Api.Endpoints.Admin.DeleteAuction;
using Auktionshuset.Api.Endpoints.Admin.GetAuctions;
using Auktionshuset.Api.Endpoints.Admin.UpdateAuction;
using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Application.Admin.Auctions.CreateAuction;
using Auktionshuset.Application.Admin.Auctions.DeleteAuction;
using Auktionshuset.Application.Admin.Auctions.GetAuctions;
using Auktionshuset.Application.Admin.Auctions.UpdateAuction;
using Auktionshuset.Contracts.Dto.Admin.Auction;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Service;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Auktionshuset.Api.Test.Admins.Auctions;

public class AuctionEndpointsTest
{
    /// <summary>
    /// Verifies that a valid request returns 201 with the location of the new auction.
    /// </summary>
    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreatedWithLocation()
    {
        var (context, employee, lot) = await CreateContextAsync(lotQuantity: 4);

        var result = await CreateAuctionEndpoint.HandleAsync(
            CreateValidRequest(employee.EmployeeId, [(lot.LotId, 2)]),
            CreateHandler(context),
            CancellationToken.None);

        var created = Assert.IsType<Created<CreateAuctionResponse>>(result.Result);
        Assert.Equal(201, created.StatusCode);
        Assert.NotEqual(Guid.Empty, created.Value!.AuctionId);
        Assert.Equal(1, created.Value.LotCount);
        Assert.Equal(2, created.Value.ItemCount);
        Assert.Equal($"/api/auctions/{created.Value.AuctionId}", created.Location);
    }

    /// <summary>
    /// Verifies that a start time in the past is rejected with a validation problem.
    /// </summary>
    [Fact]
    public async Task Create_WithPastStart_ReturnsValidationProblem()
    {
        var (context, employee, _) = await CreateContextAsync();

        var request = CreateValidRequest(employee.EmployeeId, [], startsAt: DateTime.Now.AddDays(-1));

        var result = await CreateAuctionEndpoint.HandleAsync(
            request,
            CreateHandler(context),
            CancellationToken.None);

        var problem = Assert.IsType<ValidationProblem>(result.Result);
        Assert.Contains(
            problem.ProblemDetails.Errors.SelectMany(error => error.Value),
            message => message.Contains("fremtiden"));
    }

    /// <summary>
    /// Verifies that an unknown auctionarius is rejected with a validation problem.
    /// </summary>
    [Fact]
    public async Task Create_WithUnknownEmployee_ReturnsValidationProblem()
    {
        var (context, _, _) = await CreateContextAsync();

        var result = await CreateAuctionEndpoint.HandleAsync(
            CreateValidRequest(Guid.NewGuid(), []),
            CreateHandler(context),
            CancellationToken.None);

        var problem = Assert.IsType<ValidationProblem>(result.Result);
        Assert.Contains(
            problem.ProblemDetails.Errors.SelectMany(error => error.Value),
            message => message.Contains("auktionarius"));
    }

    /// <summary>
    /// Verifies that an auction can be created without an auctionarius.
    /// </summary>
    [Fact]
    public async Task Create_WithoutEmployee_ReturnsCreatedWithoutAuctionarius()
    {
        var (context, _, _) = await CreateContextAsync();

        var result = await CreateAuctionEndpoint.HandleAsync(
            CreateValidRequest(null, []),
            CreateHandler(context),
            CancellationToken.None);

        var created = Assert.IsType<Created<CreateAuctionResponse>>(result.Result);

        var stored = await context.Auctions.GetByIdAsync(created.Value!.AuctionId, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Null(stored.EmployeeId);
    }

    /// <summary>
    /// Verifies that a quantity larger than the stock is rejected with a validation problem.
    /// </summary>
    [Fact]
    public async Task Create_WithQuantityAboveStock_ReturnsValidationProblem()
    {
        var (context, employee, lot) = await CreateContextAsync(lotQuantity: 2);

        var result = await CreateAuctionEndpoint.HandleAsync(
            CreateValidRequest(employee.EmployeeId, [(lot.LotId, 5)]),
            CreateHandler(context),
            CancellationToken.None);

        var problem = Assert.IsType<ValidationProblem>(result.Result);
        Assert.Contains(
            problem.ProblemDetails.Errors.SelectMany(error => error.Value),
            message => message.Contains("kun 2 stk."));
    }

    /// <summary>
    /// Verifies that the dashboard endpoint returns every auction with its totals and auctionarius.
    /// </summary>
    [Fact]
    public async Task GetAuctions_ReturnsEveryAuctionWithTotals()
    {
        var (context, employee, lot) = await CreateContextAsync(lotQuantity: 5);

        await CreateAuctionEndpoint.HandleAsync(
            CreateValidRequest(employee.EmployeeId, [(lot.LotId, 3)]),
            CreateHandler(context),
            CancellationToken.None);

        var result = await GetAuctionsEndpoint.HandleGetAllAsync(
            new GetAuctionsHandler(context.Auctions, context.Employees),
            CancellationToken.None);

        var row = Assert.Single(result.Value!);
        Assert.Equal(1, row.LotCount);
        Assert.Equal(3, row.ItemCount);
        Assert.Equal(employee.FullName, row.EmployeeName);
        Assert.Equal(AuctionStatuses.Upcoming, row.Status);
    }

    /// <summary>
    /// Verifies that the dashboard reports an auction without an auctionarius as not assigned.
    /// </summary>
    [Fact]
    public async Task GetAuctions_WithoutEmployee_ReportsNotAssigned()
    {
        var (context, _, _) = await CreateContextAsync();

        await CreateAuctionEndpoint.HandleAsync(
            CreateValidRequest(null, []),
            CreateHandler(context),
            CancellationToken.None);

        var result = await GetAuctionsEndpoint.HandleGetAllAsync(
            new GetAuctionsHandler(context.Auctions, context.Employees),
            CancellationToken.None);

        var row = Assert.Single(result.Value!);
        Assert.Null(row.EmployeeId);
        Assert.Equal("Ikke tildelt", row.EmployeeName);
    }

    /// <summary>
    /// Verifies that the dashboard reports the image URLs of the lots so thumbnails can be built.
    /// </summary>
    [Fact]
    public async Task GetAuctions_WithLotImage_ReturnsRelativeImageUrl()
    {
        var (context, employee, lot) = await CreateContextAsync(lotQuantity: 1);
        lot.ImageFileName = "billede.png";
        await context.Lots.UpdateAsync(lot, CancellationToken.None);

        await CreateAuctionEndpoint.HandleAsync(
            CreateValidRequest(employee.EmployeeId, [(lot.LotId, 1)]),
            CreateHandler(context),
            CancellationToken.None);

        var result = await GetAuctionsEndpoint.HandleGetAllAsync(
            new GetAuctionsHandler(context.Auctions, context.Employees),
            CancellationToken.None);

        Assert.Equal("/uploads/lots/billede.png", Assert.Single(Assert.Single(result.Value!).ImageUrls));
    }

    /// <summary>
    /// Verifies that a single auction can be loaded, and that an unknown identifier returns 404.
    /// </summary>
    [Fact]
    public async Task GetAuction_ReturnsDetailOrNotFound()
    {
        var (context, employee, lot) = await CreateContextAsync(lotQuantity: 4);
        var handler = new GetAuctionsHandler(context.Auctions, context.Employees);

        var created = await CreateAuctionEndpoint.HandleAsync(
            CreateValidRequest(employee.EmployeeId, [(lot.LotId, 4)]),
            CreateHandler(context),
            CancellationToken.None);

        var auctionId = Assert.IsType<Created<CreateAuctionResponse>>(created.Result).Value!.AuctionId;

        var found = await GetAuctionsEndpoint.HandleGetByIdAsync(auctionId, handler, CancellationToken.None);
        var detail = Assert.IsType<Ok<AuctionDetailResponse>>(found.Result).Value!;

        Assert.Equal(auctionId, detail.AuctionId);
        Assert.Equal(1, detail.LotCount);
        Assert.Equal(4, detail.ItemCount);
        Assert.Equal(lot.LotId, Assert.Single(detail.Lots).LotId);

        var missing = await GetAuctionsEndpoint.HandleGetByIdAsync(Guid.NewGuid(), handler, CancellationToken.None);
        Assert.IsType<NotFound>(missing.Result);
    }

    /// <summary>
    /// Verifies that updating an auction returns 200 with the new totals.
    /// </summary>
    [Fact]
    public async Task Update_WithValidRequest_ReturnsOk()
    {
        var (context, employee, lot) = await CreateContextAsync(lotQuantity: 6);
        var auctionId = await CreateAuctionAsync(context, employee, (lot.LotId, 1));

        var result = await UpdateAuctionEndpoint.HandleAsync(
            auctionId,
            CreateValidUpdateRequest(employee.EmployeeId, [(lot.LotId, 5)]),
            new UpdateAuctionHandler(context.Auctions, context.Lots, context.Employees, context.Publisher),
            CancellationToken.None);

        var ok = Assert.IsType<Ok<UpdateAuctionResponse>>(result.Result);
        Assert.Equal(1, ok.Value!.LotCount);
        Assert.Equal(5, ok.Value.ItemCount);
    }

    /// <summary>
    /// Verifies that an existing auctionarius is removed again when an auction is updated without one.
    /// </summary>
    [Fact]
    public async Task Update_WithoutEmployee_ClearsAuctionarius()
    {
        var (context, employee, _) = await CreateContextAsync();
        var auctionId = await CreateAuctionAsync(context, employee);

        var result = await UpdateAuctionEndpoint.HandleAsync(
            auctionId,
            CreateValidUpdateRequest(null, []),
            new UpdateAuctionHandler(context.Auctions, context.Lots, context.Employees, context.Publisher),
            CancellationToken.None);

        Assert.IsType<Ok<UpdateAuctionResponse>>(result.Result);

        var stored = await context.Auctions.GetByIdAsync(auctionId, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Null(stored.EmployeeId);
    }

    /// <summary>
    /// Verifies that updating an auction that does not exist returns 404.
    /// </summary>
    [Fact]
    public async Task Update_WithUnknownAuction_ReturnsNotFound()
    {
        var (context, employee, _) = await CreateContextAsync();

        var result = await UpdateAuctionEndpoint.HandleAsync(
            Guid.NewGuid(),
            CreateValidUpdateRequest(employee.EmployeeId, []),
            new UpdateAuctionHandler(context.Auctions, context.Lots, context.Employees, context.Publisher),
            CancellationToken.None);

        Assert.IsType<NotFound>(result.Result);
    }

    /// <summary>
    /// Verifies that an invalid update returns a validation problem.
    /// </summary>
    [Fact]
    public async Task Update_WithEndBeforeStart_ReturnsValidationProblem()
    {
        var (context, employee, _) = await CreateContextAsync();
        var auctionId = await CreateAuctionAsync(context, employee);

        var startsAt = DateTime.Now.AddDays(6);
        var request = CreateValidUpdateRequest(
            employee.EmployeeId,
            [],
            startsAt: startsAt,
            endsAt: startsAt.AddMinutes(-30));

        var result = await UpdateAuctionEndpoint.HandleAsync(
            auctionId,
            request,
            new UpdateAuctionHandler(context.Auctions, context.Lots, context.Employees, context.Publisher),
            CancellationToken.None);

        var problem = Assert.IsType<ValidationProblem>(result.Result);
        Assert.Contains(
            problem.ProblemDetails.Errors.SelectMany(error => error.Value),
            message => message.Contains("efter starttidspunktet"));
    }

    /// <summary>
    /// Verifies that deleting an auction returns 204 and that deleting it again returns 404.
    /// </summary>
    [Fact]
    public async Task Delete_ReturnsNoContentAndThenNotFound()
    {
        var (context, employee, _) = await CreateContextAsync();
        var auctionId = await CreateAuctionAsync(context, employee);
        var handler = new DeleteAuctionHandler(context.Auctions, context.Publisher);

        var deleted = await DeleteAuctionEndpoint.HandleAsync(auctionId, handler, CancellationToken.None);
        var repeated = await DeleteAuctionEndpoint.HandleAsync(auctionId, handler, CancellationToken.None);

        Assert.IsType<NoContent>(deleted.Result);
        Assert.IsType<NotFound>(repeated.Result);
        Assert.Null(await context.Auctions.GetByIdAsync(auctionId, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that a deleted auction is announced on the integration event bus.
    /// </summary>
    [Fact]
    public async Task Delete_WithExistingAuction_PublishesDeletedEvent()
    {
        var (context, employee, _) = await CreateContextAsync();
        var auctionId = await CreateAuctionAsync(context, employee);

        await DeleteAuctionEndpoint.HandleAsync(
            auctionId,
            new DeleteAuctionHandler(context.Auctions, context.Publisher),
            CancellationToken.None);

        var published = Assert.Single(context.Publisher.Published.OfType<AuctionDeletedIntegrationEvent>());
        Assert.Equal(auctionId, published.AuctionId);
    }

    /// <summary>
    /// Verifies that the dashboard also lists auctions that were never created through the endpoint,
    /// so the list is not limited to recent notifications.
    /// </summary>
    [Fact]
    public async Task GetAuctions_WithStoredAuctionThatWasNotCreatedThroughEndpoint_ListsIt()
    {
        var (context, employee, _) = await CreateContextAsync();

        await context.Auctions.AddAsync(
            new Auction
            {
                AuctionId = Guid.NewGuid(),
                Name = "Gemt i lageret",
                StartsAt = DateTime.Now.AddDays(3),
                EndsAt = DateTime.Now.AddDays(4),
                EmployeeId = employee.EmployeeId,
                AuctionHouseId = Guid.NewGuid(),
                AuctionStatus = AuctionStatuses.Upcoming
            },
            [],
            CancellationToken.None);

        var result = await GetAuctionsEndpoint.HandleGetAllAsync(
            new GetAuctionsHandler(context.Auctions, context.Employees),
            CancellationToken.None);

        Assert.Equal("Gemt i lageret", Assert.Single(result.Value!).Name);
    }

    private static CreateAuctionHandler CreateHandler(TestContext context) =>
        new(context.Auctions, context.Lots, context.Employees, context.Publisher);

    private static async Task<Guid> CreateAuctionAsync(
        TestContext context,
        EmployeeRow employee,
        params (Guid LotId, int Quantity)[] lots)
    {
        var created = await CreateAuctionEndpoint.HandleAsync(
            CreateValidRequest(employee.EmployeeId, lots),
            CreateHandler(context),
            CancellationToken.None);

        return Assert.IsType<Created<CreateAuctionResponse>>(created.Result).Value!.AuctionId;
    }

    private static CreateAuctionRequest CreateValidRequest(
        Guid? employeeId,
        (Guid LotId, int Quantity)[] lots,
        DateTime? startsAt = null,
        DateTime? endsAt = null) => new()
    {
        Name = "Forårsauktion",
        StartsAt = startsAt ?? DateTime.Now.AddDays(3),
        EndsAt = endsAt ?? DateTime.Now.AddDays(4),
        EmployeeId = employeeId,
        Lots = [.. lots.Select(lot => new AuctionLotRequest(lot.LotId, lot.Quantity))]
    };

    private static UpdateAuctionRequest CreateValidUpdateRequest(
        Guid? employeeId,
        (Guid LotId, int Quantity)[] lots,
        DateTime? startsAt = null,
        DateTime? endsAt = null) => new()
    {
        Name = "Efterårsauktion",
        StartsAt = startsAt ?? DateTime.Now.AddDays(6),
        EndsAt = endsAt ?? DateTime.Now.AddDays(7),
        EmployeeId = employeeId,
        Lots = [.. lots.Select(lot => new AuctionLotRequest(lot.LotId, lot.Quantity))]
    };

    /// <summary>
    /// Builds the repositories and a seeded genstand that the endpoint tests share.
    /// </summary>
    private static async Task<(TestContext Context, EmployeeRow Employee, Lot Lot)> CreateContextAsync(
        int lotQuantity = 1)
    {
        var employees = new InMemoryEmployeeRepository();
        var seeded = (await employees.GetAllAsync(CancellationToken.None))[0];
        var employee = new EmployeeRow(seeded.EmployeeId, $"{seeded.FirstName} {seeded.LastName}".Trim());

        var lots = new InMemoryLotRepository();
        var lot = new Lot
        {
            LotId = Guid.NewGuid(),
            Name = "Stol",
            Category = "Møbler",
            Quantity = lotQuantity,
            EstimatedValue = 500m,
            Description = "En genstand",
            Tags = ["træ"],
            AuctionHouseId = Guid.NewGuid()
        };

        await lots.AddAsync(lot, CancellationToken.None);

        return (
            new TestContext(new InMemoryAuctionRepository(), lots, employees),
            employee,
            lot);
    }

    private sealed record EmployeeRow(Guid EmployeeId, string FullName);

    private sealed record TestContext(
        InMemoryAuctionRepository Auctions,
        InMemoryLotRepository Lots,
        InMemoryEmployeeRepository Employees)
    {
        public RecordingEventPublisher Publisher { get; } = new();
    }
}
