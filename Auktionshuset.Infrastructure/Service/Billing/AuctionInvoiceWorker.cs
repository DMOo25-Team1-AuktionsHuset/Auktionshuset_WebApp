using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Auktionshuset.Infrastructure.Service.Billing;

public sealed class AuctionInvoiceWorker(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<AuctionInvoiceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Fakturabehandling fejlede; forsøger igen næste gang"); }
            try { await Task.Delay(TimeSpan.FromSeconds(5), clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    public async Task ProcessAsync(CancellationToken ct = default)
    {
        List<Guid> ids;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
            ids = await db.Auction.AsNoTracking().Where(a => a.ClosedAtTime != null
                && a.AuctionLots.Any(l => l.ClosedAtTime != null && l.CurrentHighestBidId != null
                    && !db.AuctionInvoiceLines.Any(i => i.AuctionLotId == l.AuctionLotId)))
                .OrderBy(a => a.ClosedAtTime).ThenBy(a => a.AuctionId).Select(a => a.AuctionId).ToListAsync(ct);
        }
        foreach (var id in ids)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<AuctionInvoiceStore>().IssueAsync(id, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { logger.LogError(ex, "Kunne ikke fakturere auktion {AuctionId}; forsøger igen", id); }
        }
    }
}
