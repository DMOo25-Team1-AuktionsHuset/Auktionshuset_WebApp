using Auktionshuset.Application.Abstraction;
using Auktionshuset.Application.Abstraction.Auth;
using Auktionshuset.Application.Admin.Devices;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Auktionshuset.Infrastructure.Service.Auctions;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Infrastructure.Repositories
{
    public sealed class PostgresDeviceRepository(
        AHDBContext db, TimeProvider clock, IOutboxWriter outbox) : IDeviceRepository, IDeviceAssignmentRepository
    {
        public async Task AddAsync(
            Device device,
            DeviceCredential credential,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(device);
            ArgumentNullException.ThrowIfNull(credential);

            if (device.DeviceId != credential.DeviceId)

            {
                throw new ArgumentException(
                    "Device and credential must have the same device id",
                    nameof(credential));
            }

            credential.Device = device;

            db.Set<Device>().Add(device);
            db.DeviceCredentials.Add(credential);
            await outbox.AddAsync(new DeviceRegisteredIntegrationEvent(Guid.NewGuid(), device.DeviceId,
                device.AuctionHouseId, device.DeviceNumber, clock.GetUtcNow().UtcDateTime), cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
        }

        public async Task<DeviceAssignmentResult> IssueAsync(Guid deviceId, Guid auctionId, Guid customerId, CancellationToken cancellationToken)
        {
            // SaveChanges below must not persist unrelated, possibly stale tracked changes.
            if (db.ChangeTracker.HasChanges())
                throw new InvalidOperationException("Device issuance requires a DbContext without pending changes.");

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await SqlAuctionWriteLock.AcquireAsync(db, auctionId, cancellationToken);

            var auction = await db.Auction.AsNoTracking().SingleOrDefaultAsync(a => a.AuctionId == auctionId, cancellationToken);
            if (auction is null) return new(DeviceAssignmentStatus.AuctionNotFound);
            var credential = await db.DeviceCredentials.AsNoTracking().Include(c => c.Device)
                .SingleOrDefaultAsync(c => c.DeviceId == deviceId, cancellationToken);
            if (credential is null) return new(DeviceAssignmentStatus.DeviceNotFound);
            if (!credential.IsEnabled) return new(DeviceAssignmentStatus.DeviceDisabled);
            if (credential.Device.AuctionHouseId != auction.AuctionHouseId) return new(DeviceAssignmentStatus.AuctionHouseMismatch);
            if (!await db.Customers.AnyAsync(c => c.CustomerId == customerId, cancellationToken))
                return new(DeviceAssignmentStatus.CustomerNotFound);

            DateTime now = clock.GetUtcNow().UtcDateTime;
            if (await db.DeviceSessions.AnyAsync(s => s.DeviceId == deviceId && (s.EndedAt == null || s.EndedAt > now), cancellationToken))
                return new(DeviceAssignmentStatus.DeviceAlreadyAssigned);
            if (await db.DeviceSessions.AnyAsync(s => s.AuctionId == auctionId && s.CustomerId == customerId && (s.EndedAt == null || s.EndedAt > now), cancellationToken))
                return new(DeviceAssignmentStatus.CustomerAlreadyAssigned);

            if (!await db.AuctionRegistrations.AnyAsync(r => r.AuctionId == auctionId && r.CustomerId == customerId, cancellationToken))
                db.AuctionRegistrations.Add(new AuctionRegistration { AuctionId = auctionId, CustomerId = customerId, RegisteredAt = now });

            var session = new DeviceSession
            {
                DeviceSessionId = Guid.NewGuid(), DeviceId = deviceId, AuctionId = auctionId,
                CustomerId = customerId, StartedAt = now, EndedAt = null
            };
            db.DeviceSessions.Add(session);
            await IncrementSessionRevisionAsync(deviceId, cancellationToken);
            await outbox.AddAsync(new DeviceIssuedIntegrationEvent(Guid.NewGuid(), deviceId, session.DeviceSessionId,
                auctionId, customerId, now), cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(DeviceAssignmentStatus.Success, session.DeviceSessionId);
        }

        public async Task<DeviceAssignmentResult> ReleaseAsync(Guid deviceId, Guid auctionId, Guid sessionId, CancellationToken cancellationToken)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await SqlAuctionWriteLock.AcquireAsync(db, auctionId, cancellationToken);
            var sessions = db.DeviceSessions.Where(s => s.DeviceSessionId == sessionId
                && s.DeviceId == deviceId && s.AuctionId == auctionId);
            var session = await sessions.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (session is null) return new(DeviceAssignmentStatus.SessionNotFound);
            DateTime now = clock.GetUtcNow().UtcDateTime;
            var updated = await sessions.Where(s => s.EndedAt == null || s.EndedAt > now)
                .ExecuteUpdateAsync(s => s.SetProperty(item => item.EndedAt, (DateTime?)now), cancellationToken);
            if (updated != 0)
            {
                await IncrementSessionRevisionAsync(deviceId, cancellationToken);
                await outbox.AddAsync(new DeviceReleasedIntegrationEvent(Guid.NewGuid(), deviceId, sessionId,
                    auctionId, session.CustomerId, now), cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return new(DeviceAssignmentStatus.Success, sessionId);
        }

        public async Task<DeviceAssignmentResult> DeactivateAsync(Guid deviceId, CancellationToken cancellationToken)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            // The global lock must precede reads of sessions and every auction row lock.
            if (db.Database.IsNpgsql())
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(704291031)", cancellationToken);
            else if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"DeviceCredentials\" SET \"IsEnabled\" = \"IsEnabled\" WHERE \"DeviceId\" = {deviceId}", cancellationToken);
            else
                throw new NotSupportedException("Device deactivation requires PostgreSQL or SQLite transaction locking.");

            DateTime now = clock.GetUtcNow().UtcDateTime;
            var auctionIds = await db.DeviceSessions.AsNoTracking()
                .Where(s => s.DeviceId == deviceId && (s.EndedAt == null || s.EndedAt > now))
                .Select(s => s.AuctionId).Distinct().OrderBy(id => id).ToListAsync(cancellationToken);
            foreach (Guid auctionId in auctionIds)
                await SqlAuctionWriteLock.AcquireAsync(db, auctionId, cancellationToken);

            var enabled = await db.DeviceCredentials.AsNoTracking().Where(c => c.DeviceId == deviceId)
                .Select(c => (bool?)c.IsEnabled).SingleOrDefaultAsync(cancellationToken);
            if (enabled is null) return new(DeviceAssignmentStatus.DeviceNotFound);
            var activeSessions = await db.DeviceSessions.AsNoTracking()
                .Where(s => s.DeviceId == deviceId && (s.EndedAt == null || s.EndedAt > now))
                .ToListAsync(cancellationToken);
            await db.DeviceSessions.Where(s => s.DeviceId == deviceId && (s.EndedAt == null || s.EndedAt > now))
                .ExecuteUpdateAsync(s => s.SetProperty(session => session.EndedAt, (DateTime?)now), cancellationToken);
            if (enabled.Value || activeSessions.Count != 0)
                await IncrementSessionRevisionAsync(deviceId, cancellationToken);
            foreach (var session in activeSessions)
                await outbox.AddAsync(new DeviceReleasedIntegrationEvent(Guid.NewGuid(), deviceId, session.DeviceSessionId,
                    session.AuctionId, session.CustomerId, now), cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await db.DeviceCredentials.Where(c => c.DeviceId == deviceId)
                .ExecuteUpdateAsync(c => c.SetProperty(credential => credential.IsEnabled, false), cancellationToken);
            await db.Set<Device>().Where(d => d.DeviceId == deviceId)
                .ExecuteUpdateAsync(d => d.SetProperty(device => device.Status, "Disabled"), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(DeviceAssignmentStatus.Success);
        }

        public Task<ActiveDeviceSession?> GetActiveSessionAsync(Guid deviceId, Guid? sessionId, CancellationToken cancellationToken)
        {
            DateTime now = clock.GetUtcNow().UtcDateTime;
            return (from session in db.DeviceSessions.AsNoTracking()
                    join credential in db.DeviceCredentials.AsNoTracking() on session.DeviceId equals credential.DeviceId
                    where session.DeviceId == deviceId && (sessionId == null || session.DeviceSessionId == sessionId)
                        && credential.IsEnabled && session.StartedAt <= now
                        && (session.EndedAt == null || session.EndedAt > now)
                    select new ActiveDeviceSession(session.DeviceId, session.DeviceSessionId, session.CustomerId, session.AuctionId, credential.SessionRevision))
                .SingleOrDefaultAsync(cancellationToken);
        }

        private Task<int> IncrementSessionRevisionAsync(Guid deviceId, CancellationToken cancellationToken) =>
            db.DeviceCredentials.Where(c => c.DeviceId == deviceId)
                .ExecuteUpdateAsync(c => c.SetProperty(item => item.SessionRevision, item => item.SessionRevision + 1), cancellationToken);

        public Task<DeviceCredential?> GetCredentialAsync(
            Guid deviceId,
            CancellationToken cancellationToken)
        {
            return db.DeviceCredentials
                .AsNoTracking()
                .Include(credential => credential.Device)
                .SingleOrDefaultAsync(
                    credential => credential.DeviceId == deviceId,
                    cancellationToken);
        }
    }
}
