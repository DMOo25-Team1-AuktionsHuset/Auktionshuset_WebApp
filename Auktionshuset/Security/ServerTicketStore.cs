using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Auktionshuset.Security;

public sealed class ServerTicketStore(TimeProvider timeProvider) : ITicketStore
{
    public const string SessionIdClaim = "app_session_id";
    public const string BackendTokenProperty = ".backend_access_token";

    private readonly ConcurrentDictionary<string, AuthenticationTicket> tickets = new(StringComparer.Ordinal);

    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        string sessionId = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        if (ticket.Principal.Identity is ClaimsIdentity identity)
        {
            identity.AddClaim(new Claim(SessionIdClaim, sessionId));
        }

        tickets[sessionId] = Clone(ticket);
        return Task.FromResult(sessionId);
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        if (IsValid(key))
        {
            tickets[key] = Clone(ticket);
        }

        return Task.CompletedTask;
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key) =>
        Task.FromResult(GetValidTicket(key));

    public Task RemoveAsync(string key)
    {
        tickets.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public bool IsValid(string sessionId) => GetValidTicket(sessionId) is not null;

    public AuthenticationTicket? GetValidTicket(string sessionId)
    {
        if (!tickets.TryGetValue(sessionId, out AuthenticationTicket? ticket))
        {
            return null;
        }

        DateTimeOffset? expiresAt = ticket.Properties.ExpiresUtc;
        if (expiresAt is null || expiresAt <= timeProvider.GetUtcNow())
        {
            tickets.TryRemove(sessionId, out _);
            return null;
        }

        return Clone(ticket);
    }

    public void Invalidate(string sessionId) => tickets.TryRemove(sessionId, out _);

    private static AuthenticationTicket Clone(AuthenticationTicket ticket) =>
        new(ticket.Principal, ticket.Properties, ticket.AuthenticationScheme);
}
