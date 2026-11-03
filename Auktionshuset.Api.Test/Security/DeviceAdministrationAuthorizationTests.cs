using System.Net;
using System.Net.Http.Json;
using Auktionshuset.Api.Security;
using Auktionshuset.Domain;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Auktionshuset.Infrastructure.Service.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;

namespace Auktionshuset.Api.Test.Security;

public sealed partial class CustomerProfileEndpointTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegistrationUsesDefaultHouseEvenWhenRequestIncludesAnotherHouse(bool includeHouse)
    {
        using var database = new TemporarySqliteDatabase();
        using var host = await StartServerAsync(database.Path);
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
        db.AuctionHouse.Add(new AuctionHouse
        {
            AuctionHouseId = AuctionHouseDefaults.DefaultAuctionHouseId,
            AuctionHouseName = "Test", Address = "Testvej", CVRNumber = 12345678,
            PhoneNumber = "12345678", Email = "test@example.test"
        });
        await db.SaveChangesAsync();

        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(host, SecurityRoles.Admin));
        object request = includeHouse
            ? new { deviceNumber = 42, auctionHouseId = Guid.NewGuid() }
            : new { deviceNumber = 42 };
        using var response = await client.PostAsJsonAsync("/api/staff/devices/", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        var deviceId = root.GetProperty("deviceId").GetGuid();
        var secret = root.GetProperty("secret").GetString();
        Assert.NotEqual(Guid.Empty, deviceId);
        Assert.Equal(64, secret!.Length);
        Assert.Equal(AuctionHouseDefaults.DefaultAuctionHouseId, root.GetProperty("auctionHouseId").GetGuid());
        var credential = await db.DeviceCredentials.Include(c => c.Device).SingleAsync(c => c.DeviceId == deviceId);
        Assert.Equal(AuctionHouseDefaults.DefaultAuctionHouseId, credential.Device.AuctionHouseId);
        Assert.Equal(42, credential.Device.DeviceNumber);
        Assert.True(new DeviceSecretService().VerifySecret(secret, credential.SecretHash));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(SecurityRoles.Customer)]
    [InlineData(SecurityRoles.Employee)]
    [InlineData(SecurityRoles.LotAdmin)]
    [InlineData(SecurityRoles.AuctionAdmin)]
    [InlineData(SecurityRoles.Admin)]
    public async Task DeviceAdministrationEndpointsRequireStaffRole(string? role)
    {
        using var database = new TemporarySqliteDatabase();
        using var host = await StartServerAsync(database.Path);
        using var client = host.GetTestClient();
        if (role is not null)
            client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(host, role));

        var deviceId = Guid.NewGuid();
        (HttpMethod Method, string Path, object? Body, HttpStatusCode StaffStatus)[] requests =
        [
            (HttpMethod.Get, "/api/staff/devices/customers", null, HttpStatusCode.OK),
            (HttpMethod.Get, "/api/staff/devices/auctions", null, HttpStatusCode.OK),
            (HttpMethod.Get, "/api/staff/devices/available", null, HttpStatusCode.OK),
            (HttpMethod.Get, "/api/staff/devices/sessions", null, HttpStatusCode.OK),
            (HttpMethod.Post, "/api/staff/devices/", new { deviceNumber = 0 }, HttpStatusCode.BadRequest),
            (HttpMethod.Post, $"/api/staff/devices/{deviceId}/issue", new { auctionId = Guid.Empty, customerId = Guid.Empty }, HttpStatusCode.BadRequest),
            (HttpMethod.Post, $"/api/staff/devices/{deviceId}/release", new { auctionId = Guid.Empty, sessionId = Guid.Empty }, HttpStatusCode.BadRequest)
        ];

        foreach (var (method, path, body, staffStatus) in requests)
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await client.SendAsync(request);
            var expected = role is null ? HttpStatusCode.Unauthorized
                : role == SecurityRoles.Customer ? HttpStatusCode.Forbidden : staffStatus;
            Assert.True(response.StatusCode == expected,
                $"{role ?? "Anonymous"} {method} {path}: expected {(int)expected}, got {(int)response.StatusCode}.");
        }
    }
}
