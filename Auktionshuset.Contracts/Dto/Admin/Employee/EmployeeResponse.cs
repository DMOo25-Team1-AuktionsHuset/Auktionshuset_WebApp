namespace Auktionshuset.Contracts.Dto.Admin.Employee;

public sealed record EmployeeResponse(
    Guid EmployeeId,
    string FirstName,
    string LastName,
    DateOnly BirthDate,
    string Address,
    Guid AuctionHouseId);
