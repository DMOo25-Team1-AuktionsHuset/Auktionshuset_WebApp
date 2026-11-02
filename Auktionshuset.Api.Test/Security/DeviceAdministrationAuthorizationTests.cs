using System.Net;
using System.Net.Http.Json;
using Auktionshuset.Api.Security;
using Microsoft.AspNetCore.TestHost;

namespace Auktionshuset.Api.Test.Security;

public sealed partial class CustomerProfileEndpointTests
{
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
            (HttpMethod.Post, "/api/staff/devices/", new { auctionHouseId = Guid.Empty, deviceNumber = 0 }, HttpStatusCode.BadRequest),
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
