namespace Auktionshuset.Contracts.Dto.Admin.Employee;

/// <summary>
/// A selectable employee, used as auctionarius on an auction.
/// </summary>
public sealed record EmployeeListItemResponse(Guid EmployeeId, string FullName);
