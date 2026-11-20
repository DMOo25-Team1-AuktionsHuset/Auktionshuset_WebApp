using Auktionshuset.Application.Admin.Employees;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Api.Security;

public sealed class IdentityEmployeeLoginStore(AHDBContext db, ConfiguredAuthUserStore configured,
    UserManager<LoginUser> users, LoginConnections connections) : IEmployeeLoginStore
{
    public async Task<EmployeeLoginResult> GetAsync(Guid employeeId, CancellationToken ct)
    {
        if (!await db.Employee.AnyAsync(e => e.EmployeeId == employeeId, ct))
            return new(EmployeeLoginStatus.NotFound);
        var login = await db.LoginUsers.AsNoTracking().SingleOrDefaultAsync(u => u.EmployeeId == employeeId, ct);
        return new(EmployeeLoginStatus.Success, login is null ? null : new(login.Email!, login.Role, login.IsActive));
    }

    public async Task<EmployeeLoginResult> SaveAsync(SaveEmployeeLoginCommand command, CancellationToken ct)
    {
        var employeeId = command.EmployeeId;
        string email = command.Email;
        if (await configured.FindByEmailAsync(email, ct) is not null)
            return new EmployeeLoginResult(EmployeeLoginStatus.Conflict, Detail: "E-mailadressen bruges af et konfigureret login. Vælg en anden adresse.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Serialize all staff-account changes, including concurrent attempts to remove the last admin.
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(704291032)", ct);
        else if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
            await db.Database.ExecuteSqlRawAsync("UPDATE \"AspNetUsers\" SET \"SessionVersion\" = \"SessionVersion\"", ct);
        if (!await db.Employee.AnyAsync(e => e.EmployeeId == employeeId, ct)) return new EmployeeLoginResult(EmployeeLoginStatus.NotFound);
        var login = await db.LoginUsers.SingleOrDefaultAsync(u => u.EmployeeId == employeeId, ct);
        if (await db.LoginUsers.AnyAsync(u => u.Email == email && u.EmployeeId != employeeId, ct))
            return new EmployeeLoginResult(EmployeeLoginStatus.Conflict, Detail: "E-mailadressen bruges allerede af et andet login.");
        if (login is not null && login.Email != email)
            return new EmployeeLoginResult(EmployeeLoginStatus.Invalid, Detail: "E-mailadressen på et eksisterende login kan ikke ændres her.");
        if (login is { IsActive: true, Role: SecurityRoles.Admin } && (!command.IsActive || command.Role != SecurityRoles.Admin)
            && !await db.LoginUsers.AnyAsync(u => u.UserId != login.UserId && u.IsActive && u.Role == SecurityRoles.Admin, ct))
            return new EmployeeLoginResult(EmployeeLoginStatus.Conflict, Detail: "Den sidste aktive administrator kan ikke deaktiveres eller få en anden rolle.");
        if (command.Password is not null && login is not null)
        {
            foreach (var validator in users.PasswordValidators)
                if (!(await validator.ValidateAsync(users, login, command.Password)).Succeeded)
                    return new(EmployeeLoginStatus.Invalid, Detail: "Adgangskoden opfylder ikke kravene.");
        }
        bool isNew = login is null;
        if (login is null)
        {
            if (string.IsNullOrEmpty(command.Password)) return new EmployeeLoginResult(EmployeeLoginStatus.Invalid, Detail: "Angiv en adgangskode til det nye login.");
            login = new LoginUser { UserId = Guid.NewGuid(), EmployeeId = employeeId, CustomerId = null,
                Email = email, UserName = email, Role = command.Role };
        }
        else if (login.Role != command.Role || login.IsActive != command.IsActive || command.Password is not null)
        {
            login.SessionVersion++;
            // Revocation also prevents old refresh tokens from reviving a disabled/re-enabled login.
            var tokens = await db.RefreshTokens.Where(t => t.UserId == login.UserId).ToListAsync(ct);
            foreach (var token in tokens) { token.RevokedAt = DateTimeOffset.UtcNow; token.FamilyRevokedAt = DateTimeOffset.UtcNow; }
            if (command.Password is not null)
            {
                // Use Identity validation/hashing; the enclosing transaction also protects revocation.
                login.PasswordHash = users.PasswordHasher.HashPassword(login, command.Password);
                login.SecurityStamp = Guid.NewGuid().ToString();
            }
        }
        login.UserName ??= email;
        login.Role = command.Role;
        login.IsActive = command.IsActive;
        try
        {
            IdentityResult result = isNew ? await users.CreateAsync(login, command.Password!) : await users.UpdateAsync(login);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                return new EmployeeLoginResult(EmployeeLoginStatus.Conflict, Detail: "Login kunne ikke gemmes. Hent siden igen.");
            }
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            // Database uniqueness/FKs are the final guard for concurrent account creation/deletion.
            return new EmployeeLoginResult(EmployeeLoginStatus.Conflict, Detail: "Login kunne ikke gemmes. E-mail eller medarbejdertilknytning er ændret. Hent siden igen.");
        }
        await transaction.CommitAsync(ct);
        connections.Disconnect(login.UserId);
        return new EmployeeLoginResult(EmployeeLoginStatus.Success, new EmployeeLoginDetails(login.Email!, login.Role, login.IsActive));
    }
}
