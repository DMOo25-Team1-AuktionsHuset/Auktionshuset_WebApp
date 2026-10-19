using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Auktionshuset.Security;

public sealed class ServerTicketStore(TimeProvider timeProvider) : ITicketStore
{
    public const string SessionIdClaim = "app_session_id";
    public const string BackendTokenProperty = ".backend_access_token";
    public const string RefreshTokenProperty = ".backend_refresh_token";
    public const string AccessTokenExpiryProperty = ".backend_access_expires_at";
    public const string RefreshTokenExpiryProperty = ".backend_refresh_expires_at";
    public const string ClientIpProperty = ".client_ip";

    private readonly ConcurrentDictionary<string, Session> tickets = new(StringComparer.Ordinal);

    // One lock per session serializes refresh and logout across HTTP requests and Blazor circuits.
    internal sealed class Session(AuthenticationTicket ticket)
    {
        internal readonly SemaphoreSlim Gate = new(1, 1);
        internal AuthenticationTicket Ticket = ticket;
    }

    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        string sessionId = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        if (ticket.Principal.Identity is ClaimsIdentity identity)
        {
            identity.AddClaim(new Claim(SessionIdClaim, sessionId));
        }

        tickets[sessionId] = new Session(Clone(ticket));
        return Task.FromResult(sessionId);
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        Session? session = GetSession(key);
        if (session is null) return;
        await session.Gate.WaitAsync();
        try
        {
            TryUpdate(key, session, ticket);
        }
        finally
        {
            session.Gate.Release();
        }
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
        Session? session = GetSession(sessionId);
        return session is null ? null : Clone(session.Ticket);
    }

    internal Session? GetSession(string sessionId)
    {
        if (!tickets.TryGetValue(sessionId, out Session? session))
        {
            return null;
        }

        DateTimeOffset? expiresAt = session.Ticket.Properties.ExpiresUtc;
        if (expiresAt is null || expiresAt <= timeProvider.GetUtcNow())
        {
            tickets.TryRemove(sessionId, out _);
            return null;
        }

        return session;
    }

    internal bool TryUpdate(string sessionId, Session session, AuthenticationTicket ticket)
    {
        if (!ReferenceEquals(GetSession(sessionId), session)) return false;
        session.Ticket = Clone(ticket);
        return true;
    }

    public void Invalidate(string sessionId) => tickets.TryRemove(sessionId, out _);

    private static AuthenticationTicket Clone(AuthenticationTicket ticket) =>
        new(ticket.Principal.Clone(), new AuthenticationProperties(
            new Dictionary<string, string?>(ticket.Properties.Items),
            new Dictionary<string, object?>(ticket.Properties.Parameters)), ticket.AuthenticationScheme);
}
