using Auktionshuset.Api.Security;
using Auktionshuset.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Auktionshuset.Api.Test.Admins.Auctions;

public sealed partial class AuctionStockPostgresTests
{
    [PostgresStockFact]
    public async Task IdentityMigrationPreservesCustomerAndEmployeePasswordsIdsAndStatus()
    {
        await using var db = Db();
        // Only replace this fixture's internally generated, disposable database.
        Assert.StartsWith("codex_stock_", db.Database.GetDbConnection().Database);
        await db.Database.EnsureDeletedAsync();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261102104133_PersistDeviceSessionRevision");

        var house = new AuctionHouse { AuctionHouseId = Guid.NewGuid(), AuctionHouseName = "Identity migration",
            Address = "Test", CVRNumber = 12345678, PhoneNumber = "12345678", Email = "house@example.test" };
        var employee = new Employee { EmployeeId = Guid.NewGuid(), AuctionHouseId = house.AuctionHouseId,
            AuctionHouse = house, FirstName = "Staff", LastName = "Test", Address = "Test", BirthDate = new(1990, 1, 1) };
        var customer = new Customer { CustomerId = Guid.NewGuid(), FirstName = "Customer", LastName = "Test",
            Email = "customer@example.test", Address = "Test", PhoneNumber = "12345678", BirthDate = new(1990, 1, 1) };
        db.Employee.Add(employee);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var customerUserId = Guid.NewGuid();
        var employeeUserId = Guid.NewGuid();
        var hash = PersistentAuthUserStore.HashPassword(new PasswordHasher<AuthUser>(), "legacy-password-123");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "LoginUsers" ("UserId", "CustomerId", "Email", "PasswordHash", "Role", "IsActive", "SessionVersion")
            VALUES ({customerUserId}, {customer.CustomerId}, 'customer@example.test', {hash}, 'Customer', TRUE, 3);
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "LoginUsers" ("UserId", "EmployeeId", "Email", "PasswordHash", "Role", "IsActive", "SessionVersion")
            VALUES ({employeeUserId}, {employee.EmployeeId}, 'staff@example.test', {hash}, 'Admin', FALSE, 7);
            """);

        await migrator.MigrateAsync();
        var customerLogin = await db.Users.SingleAsync(u => u.UserId == customerUserId);
        var employeeLogin = await db.Users.SingleAsync(u => u.UserId == employeeUserId);
        Assert.Equal(customer.CustomerId, customerLogin.CustomerId);
        Assert.Null(customerLogin.EmployeeId);
        Assert.Equal(employee.EmployeeId, employeeLogin.EmployeeId);
        Assert.Null(employeeLogin.CustomerId);
        Assert.Equal("Admin", employeeLogin.Role);
        Assert.False(employeeLogin.IsActive);
        Assert.Equal(7, employeeLogin.SessionVersion);
        Assert.Equal(3, customerLogin.SessionVersion);
        Assert.Equal(hash, customerLogin.PasswordHash);
        Assert.Equal(hash, employeeLogin.PasswordHash);
        Assert.Equal("CUSTOMER@EXAMPLE.TEST", customerLogin.NormalizedEmail);
        Assert.Equal(customerUserId.ToString(), customerLogin.Id);
        Assert.Equal(PasswordVerificationResult.Success,
            new PasswordHasher<LoginUser>().VerifyHashedPassword(customerLogin, customerLogin.PasswordHash!, "legacy-password-123"));
        Assert.Equal(2, await db.Users.CountAsync());
        Assert.Null(await db.Database.SqlQueryRaw<string>("SELECT to_regclass('public.\"LoginUsers\"')::text AS \"Value\"").SingleAsync());
    }
}
