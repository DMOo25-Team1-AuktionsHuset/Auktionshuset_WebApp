using System.Net;
using System.Net.Http.Json;
using Auktionshuset.Api.Security;
using Auktionshuset.Domain;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Auktionshuset.Infrastructure.Data;
using Auktionshuset.Domain;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Auktionshuset.Infrastructure.Service.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
        await SeedDefaultAuctionHouseAsync(host);
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
            (HttpMethod.Get, $"/api/staff/devices/{deviceId}/bid-requests/{Guid.NewGuid()}", null, HttpStatusCode.NotFound),
            (HttpMethod.Post, $"/api/staff/devices/{deviceId}/bid-requests/{Guid.NewGuid()}/clear-no-bid", new { }, HttpStatusCode.NotFound),
            (HttpMethod.Post, "/api/staff/devices/", new { deviceNumber = 0 }, HttpStatusCode.BadRequest),
            (HttpMethod.Post, "/api/staff/devices/station-registration", new { deviceNumber = 0 }, HttpStatusCode.BadRequest),
            (HttpMethod.Get, $"/api/staff/devices/station-pending/{new string('a', 64)}", null, HttpStatusCode.NotFound),
            (HttpMethod.Post, "/api/staff/devices/station-resume", new { deviceNumber = 0, hardwareId = new string('a', 64) }, HttpStatusCode.Conflict),
            (HttpMethod.Post, $"/api/staff/devices/{deviceId}/rotate-station-credential", new { deviceNumber = 0, hardwareId = new string('a', 64) }, HttpStatusCode.Conflict),
            (HttpMethod.Post, $"/api/staff/devices/{deviceId}/complete-station-registration", new { deviceNumber = 0 }, HttpStatusCode.Conflict),
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegisterDevice_UsesDefaultAuctionHouseEvenIfRequestSuppliesAnother(bool supplyAuctionHouseId)
    {
        using var database = new TemporarySqliteDatabase();
        using var host = await StartServerAsync(database.Path);
        await SeedDefaultAuctionHouseAsync(host);
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(host, SecurityRoles.Admin));
        object request = supplyAuctionHouseId
            ? new { deviceNumber = 42, auctionHouseId = Guid.NewGuid() }
            : new { deviceNumber = 42 };
        using var response = await client.PostAsJsonAsync("/api/staff/devices/", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var registration = await response.Content.ReadFromJsonAsync<DeviceRegistrationResponse>();
        Assert.NotNull(registration);
        Assert.NotEqual(Guid.Empty, registration.DeviceId);
        Assert.Equal(AuctionHouseDefaults.DefaultAuctionHouseId, registration.AuctionHouseId);
        Assert.Equal(42, registration.DeviceNumber);
        Assert.Equal(64, registration.Secret.Length);

        await using var verificationScope = host.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AHDBContext>();
        var stored = await verificationDb.Set<Device>().AsNoTracking()
            .SingleAsync(device => device.DeviceId == registration.DeviceId);
        Assert.Equal(AuctionHouseDefaults.DefaultAuctionHouseId, stored.AuctionHouseId);
    }

    private static async Task SeedDefaultAuctionHouseAsync(Microsoft.Extensions.Hosting.IHost host)
    {
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
            db.AuctionHouse.Add(new AuctionHouse
            {
                AuctionHouseId = AuctionHouseDefaults.DefaultAuctionHouseId,
                AuctionHouseName = "Haderslev Auktionshus",
                Address = "Auktionsvej 1, 6100 Haderslev",
                CVRNumber = 31415926,
                PhoneNumber = "+45 74 52 10 00",
                Email = "kontakt@haderslev-auktionshus.dk"
            });
            await db.SaveChangesAsync();
        }
    }

    private sealed record DeviceRegistrationResponse(Guid DeviceId, Guid AuctionHouseId, int DeviceNumber, string Secret);

    [Fact]
    public async Task StationRegistration_RepeatsSameIdentityAndBindsGrantToConnection()
    {
        using var database = new TemporarySqliteDatabase();
        using var host = await StartServerAsync(database.Path);
        await SeedDefaultAuctionHouseAsync(host);
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(host, SecurityRoles.Employee));
        var stationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var connectionId = Guid.NewGuid();
        var hardwareId = new string('a', 64);
        var presenceGrant = PresenceGrant(stationId, connectionId, hardwareId, "unconfigured");
        var request = new { stationId, connectionId, deviceNumber = 47, hardwareId, presenceGrant, auctionHouseId = Guid.NewGuid() };

        using var first = await client.PostAsJsonAsync("/api/staff/devices/station-registration", request);
        using var repeated = await client.PostAsJsonAsync("/api/staff/devices/station-registration", request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.True(first.Headers.CacheControl?.NoStore);
        var a = (await first.Content.ReadFromJsonAsync<StationResult>())!;
        var b = (await repeated.Content.ReadFromJsonAsync<StationResult>())!;
        Assert.Equal(a.DeviceId, b.DeviceId);
        Assert.Equal(a.Secret, b.Secret);
        Assert.Equal(64, a.Secret.Length);
        Assert.Equal(47, a.DeviceNumber);
        Assert.True(GrantIsValid(a, stationId, connectionId, "https://staff.example.test"));
        Assert.False(GrantIsValid(a, stationId, Guid.NewGuid(), "https://staff.example.test"));
        using (var before = await client.GetAsync("/api/staff/devices/available"))
            Assert.DoesNotContain(a.DeviceId.ToString(), await before.Content.ReadAsStringAsync());
        using var pendingResponse = await client.GetAsync($"/api/staff/devices/station-pending/{hardwareId}");
        Assert.Equal(HttpStatusCode.OK, pendingResponse.StatusCode);
        var pending = (await pendingResponse.Content.ReadFromJsonAsync<PendingResult>())!;
        Assert.Equal(a.DeviceId, pending.DeviceId);
        Assert.Equal("registration", pending.Stage);
        var reconnectedConnection = Guid.NewGuid();
        using var resumedResponse = await client.PostAsJsonAsync("/api/staff/devices/station-resume",
            new { stationId, connectionId = reconnectedConnection, deviceNumber = 47, hardwareId,
                presenceGrant = PresenceGrant(stationId, reconnectedConnection, hardwareId, "unconfigured") });
        Assert.Equal(HttpStatusCode.OK, resumedResponse.StatusCode);
        var resumed = (await resumedResponse.Content.ReadFromJsonAsync<StationResult>())!;
        Assert.Equal(a.DeviceId, resumed.DeviceId);
        Assert.Equal(a.Secret, resumed.Secret);
        Assert.True(GrantIsValid(resumed, stationId, reconnectedConnection, "https://staff.example.test"));
        using var wrongHardware = await client.PostAsJsonAsync("/api/staff/devices/station-resume",
            new { stationId, connectionId = Guid.NewGuid(), deviceNumber = 47, hardwareId = new string('b', 64), presenceGrant });
        Assert.Equal(HttpStatusCode.Conflict, wrongHardware.StatusCode);

        using var forgedConnection = await client.PostAsJsonAsync("/api/staff/devices/station-registration",
            new { stationId, connectionId = Guid.NewGuid(), deviceNumber = 48, hardwareId = new string('b', 64), presenceGrant });
        Assert.Equal(HttpStatusCode.BadRequest, forgedConnection.StatusCode);

        using var changed = await client.PostAsJsonAsync("/api/staff/devices/station-registration",
            new { stationId, connectionId, deviceNumber = 48, hardwareId, presenceGrant });
        Assert.Equal(HttpStatusCode.BadRequest, changed.StatusCode);
        var newConnection = Guid.NewGuid();
        using var reconnected = await client.PostAsJsonAsync("/api/staff/devices/station-registration",
            new { stationId, connectionId = newConnection, deviceNumber = 47, hardwareId,
                presenceGrant = PresenceGrant(stationId, newConnection, hardwareId, "unconfigured") });
        Assert.Equal(HttpStatusCode.Conflict, reconnected.StatusCode);
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
        Assert.Equal(1, await db.Set<Device>().CountAsync(d => d.DeviceId == a.DeviceId));
        var stored = await db.Set<Device>().SingleAsync(d => d.DeviceId == a.DeviceId);
        Assert.Equal(AuctionHouseDefaults.DefaultAuctionHouseId, stored.AuctionHouseId);
        Assert.Equal(47, stored.DeviceNumber);
        Assert.Equal($"Provisioning:{hardwareId}", stored.Status);
        Assert.False(await db.DeviceCredentials.AsNoTracking()
            .Where(c => c.DeviceId == a.DeviceId).Select(c => c.IsEnabled).SingleAsync());

        using var unverified = await client.PostAsJsonAsync(
            $"/api/staff/devices/{a.DeviceId}/complete-station-registration",
            new { stationId, connectionId, deviceNumber = 47, hardwareId, credentialVersion = 1, verificationGrant = "v1.0.invalid" });
        Assert.Equal(HttpStatusCode.Conflict, unverified.StatusCode);
        var proof = VerificationGrant(stationId, reconnectedConnection, a.DeviceId, 47, hardwareId, 1);
        using var completed = await client.PostAsJsonAsync(
            $"/api/staff/devices/{a.DeviceId}/complete-station-registration",
            new { stationId, connectionId = reconnectedConnection, deviceNumber = 47, hardwareId, credentialVersion = 1, verificationGrant = proof });
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        using var completedAgain = await client.PostAsJsonAsync(
            $"/api/staff/devices/{a.DeviceId}/complete-station-registration",
            new { stationId, connectionId = reconnectedConnection, deviceNumber = 47, hardwareId, credentialVersion = 1, verificationGrant = proof });
        Assert.Equal(HttpStatusCode.OK, completedAgain.StatusCode);
        using var available = await client.GetAsync("/api/staff/devices/available");
        Assert.Contains(a.DeviceId.ToString(), await available.Content.ReadAsStringAsync());
        using var missingBid = await client.GetAsync($"/api/staff/devices/{a.DeviceId}/bid-requests/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.OK, missingBid.StatusCode);
        Assert.Contains("unresolved", await missingBid.Content.ReadAsStringAsync());

        var sessionId = Guid.NewGuid();
        await using (var assignedScope = host.Services.CreateAsyncScope())
        {
            var assignedDb = assignedScope.ServiceProvider.GetRequiredService<AHDBContext>();
            var customerId = Guid.NewGuid();
            var auctionId = Guid.NewGuid();
            assignedDb.Customers.Add(new Customer { CustomerId = customerId, FirstName = "Test", LastName = "Kunde",
                Email = "repair@example.test", PhoneNumber = "12345678", BirthDate = new DateOnly(1990, 1, 1), Address = "Testvej 1" });
            assignedDb.Auction.Add(new Auction { AuctionId = auctionId,
                AuctionHouseId = AuctionHouseDefaults.DefaultAuctionHouseId, Name = "Testauktion",
                StartsAt = DateTime.UtcNow, AuctionStatus = "Live" });
            assignedDb.DeviceSessions.Add(new DeviceSession { DeviceSessionId = sessionId, DeviceId = a.DeviceId,
                CustomerId = customerId, AuctionId = auctionId, StartedAt = DateTime.UtcNow.AddMinutes(-1), EndedAt = null });
            assignedDb.DeviceBidRecoveries.Add(new DeviceBidRecovery
            {
                DeviceId = a.DeviceId, RequestId = Guid.Parse("88888888-8888-4888-8888-888888888888"),
                SessionId = sessionId, CustomerId = customerId, AuctionId = auctionId,
                AuctionLotId = Guid.NewGuid(), SessionRevision = 1, AuctionRevision = 1,
                Amount = 124m, ReportedAt = DateTime.UtcNow
            });
            await assignedDb.SaveChangesAsync();
        }
        using var deniedRotation = await client.PostAsJsonAsync(
            $"/api/staff/devices/{a.DeviceId}/rotate-station-credential",
            new { stationId, connectionId = reconnectedConnection, deviceNumber = 47, hardwareId,
                presenceGrant = PresenceGrant(stationId, reconnectedConnection, hardwareId, "configured", a.DeviceId) });
        Assert.Equal(HttpStatusCode.Conflict, deniedRotation.StatusCode);
        using var prematureClear = await client.PostAsJsonAsync(
            $"/api/staff/devices/{a.DeviceId}/bid-requests/88888888-8888-4888-8888-888888888888/clear-no-bid", new { });
        Assert.Equal(HttpStatusCode.Conflict, prematureClear.StatusCode);
        await using (var releasedScope = host.Services.CreateAsyncScope())
        {
            var releasedDb = releasedScope.ServiceProvider.GetRequiredService<AHDBContext>();
            var session = await releasedDb.DeviceSessions.SingleAsync(s => s.DeviceSessionId == sessionId);
            session.EndedAt = DateTime.UtcNow;
            releasedDb.BidCommandKeys.Add(new BidCommandKey
            {
                BidCommandKeyId = Guid.NewGuid(), CustomerId = session.CustomerId,
                DeviceSessionId = sessionId, RequestId = Guid.Parse("99999999-9999-4999-8999-999999999999"),
                AuctionLotId = Guid.NewGuid(), RequestedAmount = 123m, AuctionRevision = 1,
                Accepted = true, BidId = Guid.NewGuid(), SequenceNumber = 1, CurrentPrice = 123m,
                ErrorCode = null, ResultAuctionRevision = 2, ProcessedAt = DateTime.UtcNow
            });
            await releasedDb.SaveChangesAsync();
        }
        using var recordedBid = await client.GetAsync(
            $"/api/staff/devices/{a.DeviceId}/bid-requests/99999999-9999-4999-8999-999999999999");
        Assert.Equal(HttpStatusCode.OK, recordedBid.StatusCode);
        Assert.Contains("accepted", await recordedBid.Content.ReadAsStringAsync());
        using var unresolvedBid = await client.GetAsync(
            $"/api/staff/devices/{a.DeviceId}/bid-requests/88888888-8888-4888-8888-888888888888");
        Assert.Contains("\"canClear\":true", await unresolvedBid.Content.ReadAsStringAsync());
        using var clearedBid = await client.PostAsJsonAsync(
            $"/api/staff/devices/{a.DeviceId}/bid-requests/88888888-8888-4888-8888-888888888888/clear-no-bid", new { });
        Assert.Equal(HttpStatusCode.OK, clearedBid.StatusCode);
        using var resolvedBid = await client.GetAsync(
            $"/api/staff/devices/{a.DeviceId}/bid-requests/88888888-8888-4888-8888-888888888888");
        Assert.Contains("StaffReconciledNoBid", await resolvedBid.Content.ReadAsStringAsync());

        using var rotatedResponse = await client.PostAsJsonAsync(
            $"/api/staff/devices/{a.DeviceId}/rotate-station-credential",
            new { stationId, connectionId = reconnectedConnection, deviceNumber = 47, hardwareId,
                presenceGrant = PresenceGrant(stationId, reconnectedConnection, hardwareId, "configured", a.DeviceId) });
        Assert.Equal(HttpStatusCode.OK, rotatedResponse.StatusCode);
        var rotated = (await rotatedResponse.Content.ReadFromJsonAsync<StationResult>())!;
        Assert.Equal(a.DeviceId, rotated.DeviceId);
        Assert.NotEqual(a.Secret, rotated.Secret);
        using var repeatedRotation = await client.PostAsJsonAsync(
            $"/api/staff/devices/{a.DeviceId}/rotate-station-credential",
            new { stationId, connectionId = reconnectedConnection, deviceNumber = 47, hardwareId,
                presenceGrant = PresenceGrant(stationId, reconnectedConnection, hardwareId, "configured", a.DeviceId) });
        Assert.Equal(HttpStatusCode.Conflict, repeatedRotation.StatusCode);
        using var staleProof = await client.PostAsJsonAsync(
            $"/api/staff/devices/{a.DeviceId}/complete-station-registration",
            new { stationId, connectionId = reconnectedConnection, deviceNumber = 47, hardwareId, credentialVersion = 1, verificationGrant = proof });
        Assert.Equal(HttpStatusCode.Conflict, staleProof.StatusCode);
        using var repairPendingResponse = await client.GetAsync($"/api/staff/devices/station-pending/{hardwareId}");
        Assert.Equal("repair", (await repairPendingResponse.Content.ReadFromJsonAsync<PendingResult>())!.Stage);
        var repairConnection = Guid.NewGuid();
        using var resumedRepairResponse = await client.PostAsJsonAsync("/api/staff/devices/station-resume",
            new { stationId, connectionId = repairConnection, deviceNumber = 47, hardwareId,
                presenceGrant = PresenceGrant(stationId, repairConnection, hardwareId, "configured", a.DeviceId) });
        Assert.Equal(HttpStatusCode.OK, resumedRepairResponse.StatusCode);
        var resumedRepair = (await resumedRepairResponse.Content.ReadFromJsonAsync<StationResult>())!;
        Assert.Equal(rotated.Secret, resumedRepair.Secret);
        await using var rotatedScope = host.Services.CreateAsyncScope();
        var rotatedDb = rotatedScope.ServiceProvider.GetRequiredService<AHDBContext>();
        var rotatedCredential = await rotatedDb.DeviceCredentials.AsNoTracking().SingleAsync(c => c.DeviceId == a.DeviceId);
        var secretService = new Auktionshuset.Infrastructure.Service.Auth.DeviceSecretService();
        Assert.False(secretService.VerifySecret(a.Secret, rotatedCredential.SecretHash));
        Assert.True(secretService.VerifySecret(rotated.Secret, rotatedCredential.SecretHash));
        Assert.False(rotatedCredential.IsEnabled);
        Assert.Equal(2, rotatedCredential.CredentialVersion);
        var repairedConnection = Guid.NewGuid();
        var repairedProof = VerificationGrant(stationId, repairedConnection, a.DeviceId, 47, hardwareId, 2);
        using var repaired = await client.PostAsJsonAsync(
            $"/api/staff/devices/{a.DeviceId}/complete-station-registration",
            new { stationId, connectionId = repairedConnection, deviceNumber = 47, hardwareId, credentialVersion = 2, verificationGrant = repairedProof });
        Assert.Equal(HttpStatusCode.OK, repaired.StatusCode);
    }

    private static bool GrantIsValid(StationResult result, Guid stationId, Guid connectionId, string origin)
    {
        var parts = result.Grant.Split('.');
        if (parts.Length != 3 || parts[0] != "v1") return false;
        var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.ASCII.GetBytes(result.Secret))).ToLowerInvariant();
        var message = $"AHSTATION/1\n{stationId:D}\n{origin}\n{parts[1]}\n{connectionId:D}\n{result.DeviceId:D}\n{result.DeviceNumber}\n{digest}";
        var key = Convert.FromHexString("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF");
        var expected = Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(key,
            System.Text.Encoding.ASCII.GetBytes(message))).ToLowerInvariant();
        return expected == parts[2];
    }

    [Fact]
    public async Task PendingConfiguredDevice_CanBeExplicitlyRepairedAfterFailedNetworkCheck()
    {
        using var database = new TemporarySqliteDatabase();
        using var host = await StartServerAsync(database.Path);
        await SeedDefaultAuctionHouseAsync(host);
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(host, SecurityRoles.Employee));
        var stationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var connectionId = Guid.NewGuid();
        var hardwareId = new string('c', 64);
        using var registeredResponse = await client.PostAsJsonAsync("/api/staff/devices/station-registration",
            new { stationId, connectionId, deviceNumber = 52, hardwareId,
                presenceGrant = PresenceGrant(stationId, connectionId, hardwareId, "unconfigured") });
        Assert.Equal(HttpStatusCode.OK, registeredResponse.StatusCode);
        var registered = (await registeredResponse.Content.ReadFromJsonAsync<StationResult>())!;

        // SET was saved, but Wi-Fi/TLS verification failed; the device reports configured.
        using var repairedResponse = await client.PostAsJsonAsync(
            $"/api/staff/devices/{registered.DeviceId}/rotate-station-credential",
            new { stationId, connectionId, deviceNumber = 52, hardwareId,
                presenceGrant = PresenceGrant(stationId, connectionId, hardwareId, "configured", registered.DeviceId) });
        Assert.Equal(HttpStatusCode.OK, repairedResponse.StatusCode);
        var repaired = (await repairedResponse.Content.ReadFromJsonAsync<StationResult>())!;
        Assert.Equal(registered.DeviceId, repaired.DeviceId);
        Assert.NotEqual(registered.Secret, repaired.Secret);
        using var obsoleteProof = await client.PostAsJsonAsync(
            $"/api/staff/devices/{registered.DeviceId}/complete-station-registration",
            new { stationId, connectionId, deviceNumber = 52, hardwareId, credentialVersion = 1,
                verificationGrant = VerificationGrant(stationId, connectionId, registered.DeviceId, 52, hardwareId, 1) });
        Assert.Equal(HttpStatusCode.Conflict, obsoleteProof.StatusCode);
        using var pendingResponse = await client.GetAsync($"/api/staff/devices/station-pending/{hardwareId}");
        Assert.Equal("repair", (await pendingResponse.Content.ReadFromJsonAsync<PendingResult>())!.Stage);
        using var complete = await client.PostAsJsonAsync(
            $"/api/staff/devices/{registered.DeviceId}/complete-station-registration",
            new { stationId, connectionId, deviceNumber = 52, hardwareId, credentialVersion = 2,
                verificationGrant = VerificationGrant(stationId, connectionId, registered.DeviceId, 52, hardwareId, 2) });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
    }

    private static string VerificationGrant(Guid stationId, Guid connectionId, Guid deviceId, int number, string hardwareId, int version)
    {
        var expires = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60;
        var message = $"AHSTATION/1/VERIFIED\n{stationId:D}\nhttps://staff.example.test\n{expires}\n{connectionId:D}\n{deviceId:D}\n{number}\n{hardwareId}\n{version}";
        var key = Convert.FromHexString("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF");
        var mac = Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(key,
            System.Text.Encoding.ASCII.GetBytes(message))).ToLowerInvariant();
        return $"v1.{expires}.{mac}";
    }

    private static string PresenceGrant(Guid stationId, Guid connectionId, string hardwareId,
        string state, Guid? deviceId = null)
    {
        var expires = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60;
        var message = $"AHSTATION/1/PRESENT\n{stationId:D}\nhttps://staff.example.test\n{expires}\n" +
            $"{connectionId:D}\n{hardwareId}\n{state}\n{(deviceId.HasValue ? deviceId.Value.ToString("D") : "-")}";
        var key = Convert.FromHexString("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF");
        var mac = Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(key,
            System.Text.Encoding.ASCII.GetBytes(message))).ToLowerInvariant();
        return $"v1.{expires}.{mac}";
    }

    private sealed record StationResult(Guid DeviceId, int DeviceNumber, string Secret, string Grant);
    private sealed record PendingResult(Guid DeviceId, int DeviceNumber, string Stage);
}
