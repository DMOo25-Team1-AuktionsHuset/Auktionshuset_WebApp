namespace Auktionshuset.Contracts.Dto.Admin.Employee.CreateEmployee
{
    public sealed record CreateEmployeeNotification(
        Guid EventId,
        Guid EmployeeId,
        Guid AuctionHouseId,
        string FirstName,
        string LastName,
        DateOnly BirthDate,
        string Address,
        DateTime OccurredAt);
}
