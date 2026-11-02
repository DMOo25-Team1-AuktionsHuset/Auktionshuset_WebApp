using System.Security.Claims;
using Auktionshuset.Api.Security;
using Auktionshuset.Contracts.Dto.Customer.Request;
using Auktionshuset.Contracts.Dto.Customer.Response;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Api.Services;

public enum CustomerProfileFailure
{
    None,
    NoCustomerLink
}

public sealed record CustomerProfileResult(CustomerProfileResponse? Profile, CustomerProfileFailure Failure);

public sealed record UpdateOwnProfileResult(CustomerProfileResponse? Profile, CustomerProfileFailure Failure);

/// <summary>
/// Reads and updates the signed-in customer's own profile. The customer is always
/// resolved from the validated session (the token's user id), never from request data,
/// so a caller can only ever reach its own customer record.
/// </summary>
public sealed class CustomerProfileService(AHDBContext db)
{
    public async Task<CustomerProfileResult> GetOwnProfileAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        Customer? customer = await FindOwnCustomerAsync(user, trackChanges: false, cancellationToken);
        return customer is null
            ? new CustomerProfileResult(null, CustomerProfileFailure.NoCustomerLink)
            : new CustomerProfileResult(ToResponse(customer), CustomerProfileFailure.None);
    }

    public async Task<UpdateOwnProfileResult> UpdateOwnProfileAsync(
        ClaimsPrincipal user,
        UpdateCustomerProfileRequest request,
        CancellationToken cancellationToken)
    {
        Customer? customer = await FindOwnCustomerAsync(user, trackChanges: true, cancellationToken);
        if (customer is null)
        {
            return new UpdateOwnProfileResult(null, CustomerProfileFailure.NoCustomerLink);
        }

        // Only the four allowed fields are copied; email, role, user id, and customer id
        // are owned by the account and cannot be changed through the profile.
        customer.FirstName = request.FirstName.Trim();
        customer.LastName = request.LastName.Trim();
        customer.PhoneNumber = request.PhoneNumber.Trim();
        customer.Address = request.Address.Trim();
        await db.SaveChangesAsync(cancellationToken);

        return new UpdateOwnProfileResult(ToResponse(customer), CustomerProfileFailure.None);
    }

    private async Task<Customer?> FindOwnCustomerAsync(
        ClaimsPrincipal user,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        string? userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");
        if (!Guid.TryParse(userIdValue, out Guid userId))
        {
            return null;
        }

        LoginUser? loginUser = await db.LoginUsers.AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.UserId == userId, cancellationToken);
        if (loginUser is null || !loginUser.IsActive || loginUser.Role != SecurityRoles.Customer)
        {
            return null;
        }

        IQueryable<Customer> query = db.Customers.AsNoTracking();
        if (trackChanges)
        {
            query = db.Customers.AsTracking();
        }

        return await query.SingleOrDefaultAsync(
            entity => entity.CustomerId == loginUser.CustomerId, cancellationToken);
    }

    private static CustomerProfileResponse ToResponse(Customer customer) => new(
        customer.CustomerId,
        customer.Email,
        customer.FirstName,
        customer.LastName,
        customer.PhoneNumber,
        customer.BirthDate,
        customer.Address);
}
