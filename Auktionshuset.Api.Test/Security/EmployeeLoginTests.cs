using System.Net;
using System.Net.Http.Json;
using Auktionshuset.Api.Endpoints.Admin.Employee;
using Auktionshuset.Api.Security;
using Auktionshuset.Contracts.Dto.Admin.Employee;
using Auktionshuset.Contracts.Dto.Auth;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Auktionshuset.Api.Test.Security;

public sealed partial class CustomerProfileEndpointTests
{
    private static async Task<Guid> AddLoginEmployeeAsync(AHDBContext db)
    {
        var house = new AuctionHouse { AuctionHouseId = Guid.NewGuid(), AuctionHouseName = "Login test",
            Address = "Test", CVRNumber = 12345678, PhoneNumber = "12345678", Email = "house@example.test" };
        var employee = new Employee { EmployeeId = Guid.NewGuid(), FirstName = "Test", LastName = "Medarbejder",
            Address = "Testvej", BirthDate = new DateOnly(1990, 1, 1), AuctionHouse = house, AuctionHouseId = house.AuctionHouseId };
        db.Employee.Add(employee);
        await db.SaveChangesAsync();
        return employee.EmployeeId;
    }

    [Fact]
    public async Task StaffLoginIsEmployeeOwnedAdminManagedAndChangesInvalidateExistingTokens()
    {
        using var database = new TemporarySqliteDatabase();
        using var host = await StartServerAsync(database.Path);
        using var admin = host.GetTestClient();
        admin.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(host, SecurityRoles.Admin));
        Guid employeeId;
        await using (var scope = host.Services.CreateAsyncScope())
            employeeId = await AddLoginEmployeeAsync(scope.ServiceProvider.GetRequiredService<AHDBContext>());
        string path = $"/api/employee/{employeeId}/login";
        Assert.Null(await admin.GetFromJsonAsync<EmployeeLoginResponse>(path));
        var create = new SaveEmployeeLoginRequest(" Staff@Example.Test ", "staff-password-123", "LotAdmin", true);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(path, create)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/employee/{employeeId}")).StatusCode);
        using var staff = host.GetTestClient();
        async Task<LoginResponse> Login()
        {
            var response = await staff.PostAsJsonAsync("/api/auth/login", new { email = "staff@example.test", password = "staff-password-123" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        }
        var login = await Login();
        Assert.Equal("LotAdmin", Assert.Single(login.User.Roles));
        staff.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PutAsJsonAsync(path, create with { Role = "Admin" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/auctions/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/lots/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(path, create with { Email = "staff@example.test", Password = null, Role = "Employee" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.GetAsync("/api/lots/")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken })).StatusCode);
        login = await Login();
        staff.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        Assert.Empty(login.User.Permissions);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/lots/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/staff/invoices/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(path, create with { Email = "staff@example.test", Password = null, Role = "Employee", IsActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.GetAsync("/api/staff/invoices/")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.PostAsJsonAsync("/api/auth/login", new { email = "staff@example.test", password = "staff-password-123" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(path, create with { Email = "staff@example.test", Password = null, Role = "Employee", IsActive = true })).StatusCode);
        // The test refresh store does not share the SQL revocation table. Version validation
        // must independently reject a pre-deactivation token even after reactivation.
        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken })).StatusCode);
        await Login();
        await using var check = host.Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<AHDBContext>();
        var stored = await db.LoginUsers.SingleAsync(u => u.EmployeeId == employeeId);
        Assert.Null(stored.CustomerId);
        Assert.Empty(await db.Customers.ToListAsync());
        Assert.NotEqual("staff-password-123", stored.PasswordHash);
    }

    [Fact]
    public async Task EmployeeLoginRejectsConfiguredEmailAndCustomerRoleAndProtectsLastPersistedAdmin()
    {
        using var database = new TemporarySqliteDatabase();
        using var host = await StartServerAsync(database.Path);
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
        var id = await AddLoginEmployeeAsync(db);
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AuthUser>>();
        var configured = scope.ServiceProvider.GetRequiredService<ConfiguredAuthUserStore>();
        var request = new SaveEmployeeLoginRequest("admin@example.test", "staff-password-123", "Admin", true);
        Assert.Equal(409, ((Microsoft.AspNetCore.Http.IStatusCodeHttpResult)await EmployeeLoginEndpoints.SaveAsync(id, request, db, configured, hasher, default)).StatusCode);
        Assert.Equal(400, ((Microsoft.AspNetCore.Http.IStatusCodeHttpResult)await EmployeeLoginEndpoints.SaveAsync(id, request with { Role = "Customer" }, db, configured, hasher, default)).StatusCode);
        var empty = new ConfiguredAuthUserStore(new ConfigurationBuilder().Build(), hasher);
        Assert.IsType<Ok<EmployeeLoginResponse>>(await EmployeeLoginEndpoints.SaveAsync(id, request with { Email = "only-admin@example.test" }, db, empty, hasher, default));
        foreach (var update in new[] { request with { Email = "only-admin@example.test", Password = null, IsActive = false }, request with { Email = "only-admin@example.test", Password = null, Role = "Employee" } })
        {
            var result = await EmployeeLoginEndpoints.SaveAsync(id, update, db, empty, hasher, default);
            Assert.Equal(409, ((Microsoft.AspNetCore.Http.IStatusCodeHttpResult)result).StatusCode);
        }
        Assert.True((await db.LoginUsers.SingleAsync()).IsActive);
        Assert.Equal("Admin", (await db.LoginUsers.SingleAsync()).Role);
    }
}
