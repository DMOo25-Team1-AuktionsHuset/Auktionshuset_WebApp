using Auktionshuset.Api.Security;
using Auktionshuset.Contracts.Dto.Auth;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Api.Services;

public enum RegistrationFailure
{
    None,
    DuplicateEmail
}

public sealed record RegistrationOutcome(RegisterCustomerResponse? Account, RegistrationFailure Failure);

/// <summary>
/// Creates a permanently stored login user and its uniquely linked customer record.
/// </summary>
public sealed class CustomerRegistrationService(
    AHDBContext db,
    UserManager<LoginUser> users, ConfiguredAuthUserStore? configured = null)
{
    public async Task<RegistrationOutcome> RegisterAsync(
        RegisterCustomerRequest request,
        CancellationToken cancellationToken)
    {
        string email = PersistentAuthUserStore.NormalizeEmail(request.Email);
        if ((configured is not null && await configured.FindByEmailAsync(email, cancellationToken) is not null)
            || await db.LoginUsers.AnyAsync(user => user.Email == email, cancellationToken)
            || await db.Customers.AnyAsync(customer => customer.Email == email, cancellationToken))
        {
            return new RegistrationOutcome(null, RegistrationFailure.DuplicateEmail);
        }

        var customer = new Customer
        {
            CustomerId = Guid.NewGuid(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            PhoneNumber = request.PhoneNumber.Trim(),
            BirthDate = request.BirthDate,
            Address = request.Address.Trim()
        };
        var loginUser = new LoginUser
        {
            UserId = Guid.NewGuid(),
            Email = email,
            UserName = email,
            // Public registration always receives the Customer role; callers cannot choose it.
            Role = SecurityRoles.Customer,
            CustomerId = customer.CustomerId
        };

        db.Customers.Add(customer);
        try
        {
            // One SaveChanges persists both rows in a single transaction, so a failure
            // never leaves a partially created account behind.
            var result = await users.CreateAsync(loginUser, request.Password);
            if (!result.Succeeded)
            {
                db.ChangeTracker.Clear();
                if (result.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
                    return new RegistrationOutcome(null, RegistrationFailure.DuplicateEmail);
                throw new InvalidOperationException("Identity rejected the validated customer account.");
            }
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            // The unique indexes on login email and customer id reject concurrent duplicates.
            if (await db.LoginUsers.AsNoTracking().AnyAsync(user => user.Email == email, cancellationToken))
            {
                return new RegistrationOutcome(null, RegistrationFailure.DuplicateEmail);
            }

            throw;
        }

        return new RegistrationOutcome(new RegisterCustomerResponse(customer.CustomerId, email), RegistrationFailure.None);
    }
}
