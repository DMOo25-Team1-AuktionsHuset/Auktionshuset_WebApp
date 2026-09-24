namespace Auktionshuset.Application.Admin.Employees.UpdateEmployee
{
    public sealed record UpdateEmployeeCommand(
      Guid EmployeeId,
      string FirstName,
      string LastName,
      DateOnly BirthDate,
      string Address,
      Guid AuctionHouseId);
}
