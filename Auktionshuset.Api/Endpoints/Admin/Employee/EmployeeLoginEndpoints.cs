using System.ComponentModel.DataAnnotations;
using Auktionshuset.Api.Security;
using Auktionshuset.Contracts.Dto.Admin.Employee;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace Auktionshuset.Api.Endpoints.Admin.Employee;

public static class EmployeeLoginEndpoints
{
    public static void MapEmployeeLogins(this RouteGroupBuilder group)
    {
        group.MapGet("/{employeeId:guid}/login", async (Guid employeeId, [FromServices] AHDBContext db, CancellationToken ct) =>
        {
            if (!await db.Employee.AnyAsync(e => e.EmployeeId == employeeId, ct)) return Results.NotFound();
            var login = await db.LoginUsers.AsNoTracking().SingleOrDefaultAsync(u => u.EmployeeId == employeeId, ct);
            return login is null ? Results.Text("null", "application/json")
                : Results.Ok(new EmployeeLoginResponse(login.Email, login.Role, login.IsActive));
        }).RequireAuthorization(SecurityPolicies.Admin);
        group.MapPut("/{employeeId:guid}/login", SaveAsync).RequireAuthorization(SecurityPolicies.Admin);
    }

    public static async Task<IResult> SaveAsync(Guid employeeId, SaveEmployeeLoginRequest request,
        [FromServices] AHDBContext db, ConfiguredAuthUserStore configured, IPasswordHasher<AuthUser> hasher, CancellationToken ct,
        [FromServices] LoginConnections? connections = null)
    {
        if (request.Role is not (SecurityRoles.Admin or SecurityRoles.AuctionAdmin or SecurityRoles.LotAdmin or SecurityRoles.Employee))
            return Results.BadRequest(new { detail = "Vælg en gyldig medarbejderrolle." });
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 254 || !new EmailAddressAttribute().IsValid(request.Email))
            return Results.BadRequest(new { detail = "Angiv en gyldig e-mailadresse." });
        if (request.Password is not null && request.Password.Length is < 12 or > 128)
            return Results.BadRequest(new { detail = "Adgangskoden skal være mellem 12 og 128 tegn." });
        string email = PersistentAuthUserStore.NormalizeEmail(request.Email);
        if (await configured.FindByEmailAsync(email, ct) is not null)
            return Results.Conflict(new { detail = "E-mailadressen bruges af et konfigureret login. Vælg en anden adresse." });

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Serialize all staff-account changes, including concurrent attempts to remove the last admin.
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(704291032)", ct);
        else if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
            await db.Database.ExecuteSqlRawAsync("UPDATE \"LoginUsers\" SET \"SessionVersion\" = \"SessionVersion\"", ct);
        if (!await db.Employee.AnyAsync(e => e.EmployeeId == employeeId, ct)) return Results.NotFound();
        var login = await db.LoginUsers.SingleOrDefaultAsync(u => u.EmployeeId == employeeId, ct);
        if (await db.LoginUsers.AnyAsync(u => u.Email == email && u.EmployeeId != employeeId, ct))
            return Results.Conflict(new { detail = "E-mailadressen bruges allerede af et andet login." });
        if (login is not null && login.Email != email)
            return Results.BadRequest(new { detail = "E-mailadressen på et eksisterende login kan ikke ændres her." });
        if (login is { IsActive: true, Role: SecurityRoles.Admin } && (!request.IsActive || request.Role != SecurityRoles.Admin)
            && !configured.HasAdministrator
            && !await db.LoginUsers.AnyAsync(u => u.UserId != login.UserId && u.IsActive && u.Role == SecurityRoles.Admin, ct))
            return Results.Conflict(new { detail = "Den sidste aktive administrator kan ikke deaktiveres eller få en anden rolle." });
        if (login is null)
        {
            if (string.IsNullOrEmpty(request.Password)) return Results.BadRequest(new { detail = "Angiv en adgangskode til det nye login." });
            login = new LoginUser { UserId = Guid.NewGuid(), EmployeeId = employeeId, CustomerId = null,
                Email = email, Role = request.Role, PasswordHash = PersistentAuthUserStore.HashPassword(hasher, request.Password) };
            db.LoginUsers.Add(login);
        }
        else if (login.Role != request.Role || login.IsActive != request.IsActive || request.Password is not null)
        {
            login.SessionVersion++;
            // Revocation also prevents old refresh tokens from reviving a disabled/re-enabled login.
            var tokens = await db.RefreshTokens.Where(t => t.UserId == login.UserId).ToListAsync(ct);
            foreach (var token in tokens) { token.RevokedAt = DateTimeOffset.UtcNow; token.FamilyRevokedAt = DateTimeOffset.UtcNow; }
            if (request.Password is not null) login.PasswordHash = PersistentAuthUserStore.HashPassword(hasher, request.Password);
        }
        login.Role = request.Role;
        login.IsActive = request.IsActive;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);
            // Database uniqueness/FKs are the final guard for concurrent account creation/deletion.
            return Results.Conflict(new { detail = "Login kunne ikke gemmes. E-mail eller medarbejdertilknytning er ændret. Hent siden igen." });
        }
        await transaction.CommitAsync(ct);
        connections?.Disconnect(login.UserId);
        return Results.Ok(new EmployeeLoginResponse(login.Email, login.Role, login.IsActive));
    }
}
