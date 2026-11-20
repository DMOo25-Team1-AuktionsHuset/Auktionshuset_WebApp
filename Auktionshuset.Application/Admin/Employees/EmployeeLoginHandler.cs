using System.ComponentModel.DataAnnotations;

namespace Auktionshuset.Application.Admin.Employees;

public sealed record SaveEmployeeLoginCommand(Guid EmployeeId, string Email, string? Password, string Role, bool IsActive);
public sealed record EmployeeLoginDetails(string Email, string Role, bool IsActive);
public enum EmployeeLoginStatus { Success, NotFound, Invalid, Conflict }
public sealed record EmployeeLoginResult(EmployeeLoginStatus Status, EmployeeLoginDetails? Login = null, string? Detail = null);

public interface IEmployeeLoginStore
{
    Task<EmployeeLoginResult> GetAsync(Guid employeeId, CancellationToken ct);
    Task<EmployeeLoginResult> SaveAsync(SaveEmployeeLoginCommand command, CancellationToken ct);
}

public sealed class EmployeeLoginHandler(IEmployeeLoginStore store)
{
    public Task<EmployeeLoginResult> GetAsync(Guid employeeId, CancellationToken ct) => store.GetAsync(employeeId, ct);

    public Task<EmployeeLoginResult> HandleAsync(SaveEmployeeLoginCommand command, CancellationToken ct)
    {
        string? error = command.Role is not ("Admin" or "AuctionAdmin" or "LotAdmin" or "Employee")
            ? "Vælg en gyldig medarbejderrolle."
            : string.IsNullOrWhiteSpace(command.Email) || command.Email.Length > 254 || !new EmailAddressAttribute().IsValid(command.Email.Trim())
                ? "Angiv en gyldig e-mailadresse."
                : command.Password is not null && command.Password.Length is < 12 or > 128
                    ? "Adgangskoden skal være mellem 12 og 128 tegn." : null;
        return error is not null
            ? Task.FromResult(new EmployeeLoginResult(EmployeeLoginStatus.Invalid, Detail: error))
            : store.SaveAsync(command with { Email = command.Email.Trim().ToLowerInvariant() }, ct);
    }
}
