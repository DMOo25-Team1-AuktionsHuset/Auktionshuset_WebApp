namespace Auktionshuset.Application.Admin.Employees.CreateEmployee
{
    public sealed record CreateEmployeeCommand(
        string FirstName,
        string LastName,
        DateOnly BirthDate,
        string Address,
        Guid AuctionHouseId);
}