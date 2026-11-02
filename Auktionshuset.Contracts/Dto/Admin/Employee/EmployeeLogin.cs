namespace Auktionshuset.Contracts.Dto.Admin.Employee;

public sealed record EmployeeLoginResponse(string Email, string Role, bool IsActive);
public sealed record SaveEmployeeLoginRequest(string Email, string? Password, string Role, bool IsActive);
