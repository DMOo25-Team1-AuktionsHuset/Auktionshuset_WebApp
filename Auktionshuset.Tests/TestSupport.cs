using Auktionshuset.Application.Abstraction.Admin.Employees;
using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Application.Admin.Auctions.CreateAuction;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Service;

namespace Auktionshuset.Tests;

/// <summary>
/// A controllable employee store for the handler tests.
/// </summary>
internal sealed class TestEmployeeRepository : IEmployeeRepository
{
    private readonly Dictionary<Guid, Employee> employees = [];

    public void Add(Employee employee) => employees[employee.EmployeeId] = employee;

    public Task<IReadOnlyList<Employee>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<Employee> all = employees.Values
            .OrderBy(employee => employee.FirstName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(employee => employee.LastName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        return Task.FromResult(all);
    }

    public Task<Employee?> GetByIdAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        employees.TryGetValue(employeeId, out var employee);

        return Task.FromResult(employee);
    }
}

/// <summary>
/// Records every integration event a handler publishes.
/// </summary>
internal sealed class RecordingEventPublisher : IIntegrationEventPublisher
{
    public List<IIntegrationEvent> Published { get; } = [];

    /// <summary>
    /// Gets the last published event of the requested type, or <see langword="null"/> when none was published.
    /// </summary>
    public TEvent? LastOf<TEvent>() where TEvent : class, IIntegrationEvent =>
        Published.OfType<TEvent>().LastOrDefault();

    public Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        Published.Add(message);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Builds the entities and commands the auction and lot tests share.
/// </summary>
internal static class TestData
{
    public static readonly Guid AuctionHouseId = Guid.Parse("4d1c8f2b-6a35-4e70-9b18-2c6f5a3d8e91");

    public static AuctionHouse CreateAuctionHouse() => new()
    {
        AuctionHouseId = AuctionHouseId,
        AuctionHouseName = "Haderslev Auktionshus",
        Address = "Auktionsvej 1, 6100 Haderslev",
        CVRNumber = 31415926,
        PhoneNumber = "+45 74 52 10 00",
        Email = "kontakt@haderslev-auktionshus.dk"
    };

    public static Employee CreateEmployee(string firstName = "Mette", string lastName = "Jørgensen") => new()
    {
        EmployeeId = Guid.NewGuid(),
        AuctionHouseId = CreateAuctionHouse(),
        FirstName = firstName,
        LastName = lastName,
        BirthDate = new DateOnly(1979, 4, 12),
        Address = "Strandgade 14, 6100 Haderslev"
    };

    public static Lot CreateLot(string name = "Stol", int quantity = 1, string? imageFileName = null) => new()
    {
        LotId = Guid.NewGuid(),
        Name = name,
        Category = "Møbler",
        Quantity = quantity,
        EstimatedValue = 500m,
        Description = "En genstand",
        Tags = ["træ"],
        ImageFileName = imageFileName,
        AuctionHouseId = AuctionHouseId
    };

    public static async Task<InMemoryLotRepository> CreateLotRepositoryAsync(params Lot[] lots)
    {
        var repository = new InMemoryLotRepository();

        foreach (var lot in lots)
        {
            await repository.AddAsync(lot, CancellationToken.None);
        }

        return repository;
    }

    public static CreateAuctionCommand CreateCommand(
        Guid? employeeId,
        DateTime startsAt,
        DateTime endsAt,
        params AuctionLotSelection[] lots) => new(
            Name: "Forårsauktion",
            StartsAt: startsAt,
            EndsAt: endsAt,
            EmployeeId: employeeId,
            AuctionHouseId: AuctionHouseId,
            Lots: lots);
}
