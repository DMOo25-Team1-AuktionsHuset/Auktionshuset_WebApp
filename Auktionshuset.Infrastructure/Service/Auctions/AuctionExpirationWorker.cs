using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Auktionshuset.Infrastructure.Service.Auctions;

public sealed class AuctionExpirationWorker(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<AuctionExpirationWorker> logger) : BackgroundService
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Scan immediately, including deadlines missed while the server was down.
                await ProcessDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to discover expired auctions; retrying next scan");
            }

            try { await Task.Delay(PollInterval, clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    public async Task ProcessDueAsync(CancellationToken cancellationToken)
    {
        List<Guid> ids;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
            var now = clock.GetUtcNow().UtcDateTime;
            ids = await db.Auction.AsNoTracking()
                .Where(a => a.StartedAtTime != null && a.ClosedAtTime == null
                    && a.AuctionStatus == AuctionStatuses.Live && a.EndedAt != null && a.EndedAt <= now)
                .OrderBy(a => a.EndedAt).ThenBy(a => a.AuctionId)
                .Select(a => a.AuctionId).ToListAsync(cancellationToken);
        }

        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // Never reuse tracked entities or failed transactions between closes.
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<AuctionLifeCycleStore>()
                    .CloseDueAsync(id, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to close expired auction {AuctionId}; retrying next scan", id);
            }
        }
    }
}
