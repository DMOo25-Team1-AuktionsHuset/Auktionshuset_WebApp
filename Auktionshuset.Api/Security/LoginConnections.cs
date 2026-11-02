using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Auktionshuset.Api.Security;

// Closing active hub connections prevents a demoted/disabled login from continuing to
// receive staff events until its JWT expires. HTTP requests validate the session version.
public sealed class LoginConnections : IHubFilter
{
    private readonly ConcurrentDictionary<string, (Guid UserId, Action Abort)> connections = new();

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        try
        {
            var id = context.Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(id, out var userId))
            {
                connections[context.Context.ConnectionId] = (userId, context.Context.Abort);
                // Register before revalidating, so a simultaneous account change either
                // aborts this connection or is observed here after it has committed.
                var store = context.ServiceProvider.GetRequiredService<IAuthUserStore>();
                var current = await store.FindByIdAsync(userId, context.Context.ConnectionAborted);
                if (current is null || context.Context.User?.FindFirstValue("session_version") != current.SessionVersion.ToString(System.Globalization.CultureInfo.InvariantCulture))
                {
                    connections.TryRemove(context.Context.ConnectionId, out _);
                    context.Context.Abort();
                    return;
                }
            }
            await next(context);
        }
        catch { connections.TryRemove(context.Context.ConnectionId, out _); throw; }
    }

    public async Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception,
        Func<HubLifetimeContext, Exception?, Task> next)
    {
        connections.TryRemove(context.Context.ConnectionId, out _);
        await next(context, exception);
    }

    public void Disconnect(Guid userId)
    {
        foreach (var pair in connections)
            if (pair.Value.UserId == userId) pair.Value.Abort();
    }
}
