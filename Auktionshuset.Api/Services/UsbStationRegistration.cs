using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using Auktionshuset.Application.Abstraction.Auth;
using Auktionshuset.Domain;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Api.Services;

// The physical station connection is the retry key. The secret can be recovered
// without storing it in plaintext or creating another device after a lost response.
public sealed class UsbStationRegistration(
    IConfiguration configuration, AHDBContext db, IDeviceRepository repository,
    IDeviceSecretService secrets, TimeProvider clock)
{
    public async Task<StationRegistrationResult?> RegisterAsync(StationRegistrationRequest request, CancellationToken ct)
    {
        var section = configuration.GetSection("UsbStation");
        var stationId = section["StationId"];
        var origin = section["ApprovedOrigin"];
        var keyHex = section["GrantKeyHex"];
        if (!Guid.TryParse(stationId, out var configuredStation) || stationId != configuredStation.ToString("D") ||
            !Uri.TryCreate(origin, UriKind.Absolute, out var approvedOrigin) ||
            approvedOrigin.Scheme != Uri.UriSchemeHttps || approvedOrigin.AbsoluteUri.TrimEnd('/') != origin ||
            keyHex is not { Length: 64 } || !keyHex.All(Uri.IsHexDigit))
            return null;

        if (request.DeviceNumber <= 0 || request.StationId != configuredStation || request.ConnectionId == Guid.Empty ||
            !ValidHardwareId(request.HardwareId))
            throw new ArgumentException("Invalid station registration request.");

        var key = Convert.FromHexString(keyHex);
        if (!ValidPresence(key, stationId, origin, request, "unconfigured", "-"))
            throw new ArgumentException("Station presence is invalid.");
        var operation = $"AHSTATION/1/REGISTRATION\n{request.StationId:D}\n{request.ConnectionId:D}";
        var idBytes = HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(operation + "\nID"));
        var deviceId = new Guid(idBytes.AsSpan(0, 16));
        var secret = SecretFor(key, deviceId, 1);
        var stored = await db.Set<Device>().AsNoTracking().SingleOrDefaultAsync(d => d.DeviceId == deviceId, ct);
        if (stored is null)
        {
            if (!await db.AuctionHouse.AnyAsync(a => a.AuctionHouseId == AuctionHouseDefaults.DefaultAuctionHouseId, ct))
                throw new InvalidOperationException("Default auction house is missing.");
            if (await db.Set<Device>().AsNoTracking().AnyAsync(d =>
                    d.AuctionHouseId == AuctionHouseDefaults.DefaultAuctionHouseId &&
                    (d.DeviceNumber == request.DeviceNumber ||
                     d.Status == $"Provisioning:{request.HardwareId}" ||
                     d.Status == $"RepairPending:{request.HardwareId}"), ct))
                throw new StationDeviceNumberConflictException();
            var device = new Device { DeviceId = deviceId, DeviceNumber = request.DeviceNumber,
                AuctionHouseId = AuctionHouseDefaults.DefaultAuctionHouseId, Status = $"Provisioning:{request.HardwareId}" };
            var credential = new DeviceCredential { DeviceId = deviceId, Device = device,
                SecretHash = secrets.HashSecret(secret), IsEnabled = false, CredentialVersion = 1 };
            try { await repository.AddAsync(device, credential, ct); }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                stored = await db.Set<Device>().AsNoTracking().SingleOrDefaultAsync(d => d.DeviceId == deviceId, ct);
                if (stored is null) throw;
            }
        }

        if (stored is not null && (stored.DeviceNumber != request.DeviceNumber ||
            stored.AuctionHouseId != AuctionHouseDefaults.DefaultAuctionHouseId ||
            stored.Status != $"Provisioning:{request.HardwareId}"))
            throw new ArgumentException("USB connection already belongs to a different device.");
        var credentialStored = await db.DeviceCredentials.AsNoTracking().SingleAsync(c => c.DeviceId == deviceId, ct);
        if (credentialStored.IsEnabled || !string.Equals(credentialStored.SecretHash, secrets.HashSecret(secret), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Registration credential has changed.");

        return Result(key, stationId, origin, deviceId, request.DeviceNumber, secret, request.ConnectionId);
    }

    public async Task<PendingStationDevice?> PendingAsync(string hardwareId, CancellationToken ct)
    {
        if (!ValidHardwareId(hardwareId)) return null;
        return await db.Set<Device>().AsNoTracking()
            .Where(d => d.AuctionHouseId == AuctionHouseDefaults.DefaultAuctionHouseId &&
                (d.Status == $"Provisioning:{hardwareId}" || d.Status == $"RepairPending:{hardwareId}"))
            .Select(d => new PendingStationDevice(d.DeviceId, d.DeviceNumber,
                d.Status.StartsWith("RepairPending:") ? "repair" : "registration"))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<StationRegistrationResult?> ResumeAsync(StationRegistrationRequest request, CancellationToken ct)
    {
        var pending = await PendingAsync(request.HardwareId, ct);
        var stationId = configuration["UsbStation:StationId"];
        var origin = configuration["UsbStation:ApprovedOrigin"];
        var keyHex = configuration["UsbStation:GrantKeyHex"];
        if (pending is null || pending.DeviceNumber != request.DeviceNumber || request.ConnectionId == Guid.Empty ||
            request.StationId.ToString("D") != stationId || origin is null ||
            keyHex is not { Length: 64 } || !keyHex.All(Uri.IsHexDigit)) return null;
        var credential = await db.DeviceCredentials.AsNoTracking().SingleAsync(c => c.DeviceId == pending.DeviceId, ct);
        if (credential.IsEnabled) return null;
        var key = Convert.FromHexString(keyHex);
        var state = pending.Stage == "repair" ? "configured" : "unconfigured";
        if (!ValidPresence(key, stationId!, origin, request, state,
            state == "configured" ? pending.DeviceId.ToString("D") : "-")) return null;
        var secret = SecretFor(key, pending.DeviceId, credential.CredentialVersion);
        if (!string.Equals(secrets.HashSecret(secret), credential.SecretHash, StringComparison.OrdinalIgnoreCase)) return null;
        return Result(key, stationId!, origin, pending.DeviceId, pending.DeviceNumber, secret, request.ConnectionId);
    }

    public async Task<StationRegistrationResult?> RotateAsync(Guid deviceId, StationRegistrationRequest request, CancellationToken ct)
    {
        var stationId = configuration["UsbStation:StationId"];
        var origin = configuration["UsbStation:ApprovedOrigin"];
        var keyHex = configuration["UsbStation:GrantKeyHex"];
        if (!ValidHardwareId(request.HardwareId) || request.ConnectionId == Guid.Empty ||
            request.StationId.ToString("D") != stationId || origin is null ||
            keyHex is not { Length: 64 } || !keyHex.All(Uri.IsHexDigit)) return null;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(704291031)", ct);
        else if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"DeviceCredentials\" SET \"IsEnabled\" = \"IsEnabled\" WHERE \"DeviceId\" = {deviceId}", ct);
        else throw new NotSupportedException("Credential rotation requires PostgreSQL or SQLite locking.");
        var credential = await db.DeviceCredentials.Include(c => c.Device)
            .SingleOrDefaultAsync(c => c.DeviceId == deviceId, ct);
        if (credential is null || credential.Device.DeviceNumber != request.DeviceNumber ||
            credential.Device.AuctionHouseId != AuctionHouseDefaults.DefaultAuctionHouseId ||
            credential.CredentialVersion >= 65535 ||
            !((credential.IsEnabled && (credential.Device.Status == "Available" ||
                credential.Device.Status == $"Available:{request.HardwareId}")) ||
              (!credential.IsEnabled && credential.Device.Status == $"Provisioning:{request.HardwareId}"))) return null;
        var now = clock.GetUtcNow().UtcDateTime;
        if (await db.DeviceSessions.AnyAsync(s => s.DeviceId == deviceId && s.StartedAt <= now &&
            (s.EndedAt == null || s.EndedAt > now), ct)) throw new StationDeviceAssignedException();
        var key = Convert.FromHexString(keyHex);
        if (!ValidPresence(key, stationId!, origin, request, "configured", deviceId.ToString("D"))) return null;
        var nextVersion = checked(credential.CredentialVersion + 1);
        var secret = SecretFor(key, deviceId, nextVersion);
        credential.SecretHash = secrets.HashSecret(secret);
        credential.CredentialVersion = nextVersion;
        credential.SessionRevision++;
        credential.IsEnabled = false;
        credential.Device.Status = $"RepairPending:{request.HardwareId}";
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result(key, stationId!, origin, deviceId, request.DeviceNumber, secret, request.ConnectionId);
    }

    public async Task<bool> CompleteAsync(Guid deviceId, StationCompletionRequest request, CancellationToken ct)
    {
        var stationId = configuration["UsbStation:StationId"];
        var origin = configuration["UsbStation:ApprovedOrigin"];
        var keyHex = configuration["UsbStation:GrantKeyHex"];
        if (stationId is null || origin is null || request.StationId.ToString("D") != stationId ||
            request.ConnectionId == Guid.Empty || request.DeviceNumber <= 0 || request.CredentialVersion <= 0 ||
            !ValidHardwareId(request.HardwareId) ||
            keyHex is not { Length: 64 } || !keyHex.All(Uri.IsHexDigit)) return false;
        var grantParts = request.VerificationGrant?.Split('.');
        if (grantParts is not { Length: 3 } || grantParts[0] != "v1" ||
            !long.TryParse(grantParts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var expires) ||
            expires < clock.GetUtcNow().ToUnixTimeSeconds() ||
            expires > clock.GetUtcNow().ToUnixTimeSeconds() + 120 ||
            grantParts[2].Length != 64 || !grantParts[2].All(Uri.IsHexDigit)) return false;
        var signed = $"AHSTATION/1/VERIFIED\n{stationId}\n{origin}\n{expires}\n{request.ConnectionId:D}\n{deviceId:D}\n{request.DeviceNumber}\n{request.HardwareId}\n{request.CredentialVersion}";
        var key = Convert.FromHexString(keyHex);
        var expected = HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(signed));
        if (!CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(grantParts[2]))) return false;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(704291031)", ct);
        else if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"DeviceCredentials\" SET \"IsEnabled\" = \"IsEnabled\" WHERE \"DeviceId\" = {deviceId}", ct);
        else throw new NotSupportedException("Station completion requires PostgreSQL or SQLite locking.");
        var credential = await db.DeviceCredentials.Include(c => c.Device)
            .SingleOrDefaultAsync(c => c.DeviceId == deviceId, ct);
        if (credential is null || credential.CredentialVersion != request.CredentialVersion ||
            credential.Device.DeviceNumber != request.DeviceNumber ||
            credential.Device.AuctionHouseId != AuctionHouseDefaults.DefaultAuctionHouseId ||
            credential.Device.Status is not ("Provisioning" or "Available") &&
            credential.Device.Status != $"Provisioning:{request.HardwareId}" &&
            credential.Device.Status != $"RepairPending:{request.HardwareId}" &&
            credential.Device.Status != $"Available:{request.HardwareId}") return false;
        if (!credential.IsEnabled && !credential.Device.Status.StartsWith("Provisioning:") &&
            !credential.Device.Status.StartsWith("RepairPending:")) return false;
        if (!credential.IsEnabled)
        {
            credential.IsEnabled = true;
            credential.Device.Status = $"Available:{request.HardwareId}";
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return true;
    }

    private static bool ValidHardwareId(string? value) => value is { Length: 64 } &&
        value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private bool ValidPresence(byte[] key, string stationId, string origin,
        StationRegistrationRequest request, string state, string deviceId)
    {
        var parts = request.PresenceGrant?.Split('.');
        if (parts is not { Length: 3 } || parts[0] != "v1" ||
            !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var expires) ||
            expires < clock.GetUtcNow().ToUnixTimeSeconds() ||
            expires > clock.GetUtcNow().ToUnixTimeSeconds() + 120 ||
            parts[2].Length != 64 || !parts[2].All(Uri.IsHexDigit)) return false;
        var signed = $"AHSTATION/1/PRESENT\n{stationId}\n{origin}\n{expires}\n{request.ConnectionId:D}\n{request.HardwareId}\n{state}\n{deviceId}";
        var expected = HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(signed));
        return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(parts[2]));
    }

    private static string SecretFor(byte[] key, Guid deviceId, int version) =>
        Convert.ToHexString(HMACSHA256.HashData(key,
            Encoding.ASCII.GetBytes($"AHSTATION/1/SECRET\n{deviceId:D}\n{version}")));

    private StationRegistrationResult Result(byte[] key, string stationId, string origin,
        Guid deviceId, int number, string secret, Guid connectionId)
    {
        var expires = clock.GetUtcNow().ToUnixTimeSeconds() + 90;
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(secret))).ToLowerInvariant();
        var signed = $"AHSTATION/1\n{stationId}\n{origin}\n{expires}\n{connectionId:D}\n{deviceId:D}\n{number}\n{digest}";
        var mac = Convert.ToHexString(HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(signed))).ToLowerInvariant();
        return new(deviceId, number, secret, $"v1.{expires}.{mac}");
    }
}

public sealed record StationRegistrationRequest(Guid StationId, Guid ConnectionId, int DeviceNumber, string HardwareId, string PresenceGrant);
public sealed record StationRegistrationResult(Guid DeviceId, int DeviceNumber, string Secret, string Grant);
public sealed record StationCompletionRequest(Guid StationId, Guid ConnectionId, int DeviceNumber, string HardwareId, int CredentialVersion, string VerificationGrant);
public sealed record PendingStationDevice(Guid DeviceId, int DeviceNumber, string Stage);
public sealed class StationDeviceNumberConflictException : Exception;
public sealed class StationDeviceAssignedException : Exception;
