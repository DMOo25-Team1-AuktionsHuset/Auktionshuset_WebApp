using Auktionshuset.Application.Abstraction.Admin.Auctions;
using Auktionshuset.Application.Abstraction.Admin.Employees;

namespace Auktionshuset.Application.Admin.Auctions.GetAuctions;

public sealed class GetAuctionsHandler(
    IAuctionRepository auctionRepository,
    IEmployeeRepository employeeRepository)
{
    /// <summary>
    /// Gets every auction as a dashboard row, newest start time first.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A read-only list of every auction with its auctionarius and lot totals.</returns>
    public async Task<IReadOnlyList<AuctionOverview>> HandleAsync(CancellationToken cancellationToken)
    {
        var auctions = await auctionRepository.GetAllAsync(cancellationToken);
        var employees = await employeeRepository.GetAllAsync(cancellationToken);
        var employeesById = employees.ToDictionary(employee => employee.EmployeeId);

        var overviews = new List<AuctionOverview>(auctions.Count);

        foreach (var auction in auctions)
        {
            var auctionLots = await auctionRepository.GetAuctionLotsAsync(auction.AuctionId, cancellationToken);

            overviews.Add(new AuctionOverview(
                Auction: auction,
                EmployeeName: ResolveEmployeeName(auction, employeesById),
                LotCount: auctionLots.Count,
                ItemCount: auctionLots.Sum(auctionLot => auctionLot.Quantity),
                ImageFileNames: auctionLots
                    .Select(auctionLot => auctionLot.Lot.ImageFileName)
                    .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
                    .Select(fileName => fileName!)
                    .ToArray()));
        }

        return overviews;
    }

    /// <summary>
    /// Gets the full detail view of a single auction.
    /// </summary>
    /// <param name="auctionId">The identifier of the auction to load.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The auction detail, or <see langword="null"/> when no auction has that identifier.</returns>
    public async Task<AuctionDetail?> HandleByIdAsync(Guid auctionId, CancellationToken cancellationToken)
    {
        var auction = await auctionRepository.GetByIdAsync(auctionId, cancellationToken);

        if (auction is null)
        {
            return null;
        }

        var auctionLots = await auctionRepository.GetAuctionLotsAsync(auctionId, cancellationToken);
        var employees = await employeeRepository.GetAllAsync(cancellationToken);
        var employeesById = employees.ToDictionary(employee => employee.EmployeeId);

        var lines = auctionLots
            .Select(auctionLot => new AuctionLotLine(
                LotId: auctionLot.LotId,
                Name: auctionLot.Lot.Name,
                Category: auctionLot.Lot.Category,
                Quantity: auctionLot.Quantity,
                EstimatedValue: auctionLot.Lot.EstimatedValue,
                ImageFileName: auctionLot.Lot.ImageFileName))
            .ToArray();

        return new AuctionDetail(
            Auction: auction,
            EmployeeName: ResolveEmployeeName(auction, employeesById),
            LotCount: lines.Length,
            ItemCount: lines.Sum(line => line.Quantity),
            Lots: lines);
    }

    /// <summary>
    /// Resolves the name the dashboard shows for the auctionarius. An auction without an employee is
    /// reported as not assigned, so the dashboard never shows an empty cell.
    /// </summary>
    /// <param name="auction">The auction whose auctionarius is resolved.</param>
    /// <param name="employeesById">Every stored employee, indexed by identifier.</param>
    /// <returns>The employee's full name, or a Danish fallback when none is assigned.</returns>
    private static string ResolveEmployeeName(
        Domain.Entities.Auction auction,
        IReadOnlyDictionary<Guid, Domain.Entities.Employee> employeesById)
    {
        if (auction.Employee?.FirstName is { Length: > 0 } firstName)
        {
            return $"{firstName} {auction.Employee.LastName}".Trim();
        }

        if (auction.EmployeeId is { } employeeId
            && employeesById.TryGetValue(employeeId, out var employee))
        {
            return $"{employee.FirstName} {employee.LastName}".Trim();
        }

        return "Ikke tildelt";
    }
}
