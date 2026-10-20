using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Auktionshuset.Contracts.Dto.Auth;
using Microsoft.AspNetCore.Authentication;

namespace Auktionshuset.Security;

public sealed class BackendSessionService(
    ServerTicketStore tickets, IHttpClientFactory clients, TimeProvider clock,
    ILogger<BackendSessionService> logger)
{
    public async Task<string?> GetAccessTokenAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ServerTicketStore.Session? session = tickets.GetSession(sessionId);
        if (session is null) return null;
        await session.Gate.WaitAsync(cancellationToken);
        try
        {
            AuthenticationTicket? ticket = tickets.GetValidTicket(sessionId);
            if (ticket is null) return null;
            ticket.Properties.Items.TryGetValue(ServerTicketStore.BackendTokenProperty, out string? accessToken);
            ticket.Properties.Items.TryGetValue(ServerTicketStore.RefreshTokenProperty, out string? refreshToken);
            DateTimeOffset expiresAt = ticket.Properties.Items.TryGetValue(ServerTicketStore.AccessTokenExpiryProperty, out string? expiry)
                && DateTimeOffset.TryParse(expiry, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed : ticket.Properties.ExpiresUtc!.Value;
            if (expiresAt > clock.GetUtcNow().AddSeconds(30)
                || (string.IsNullOrEmpty(refreshToken) && expiresAt > clock.GetUtcNow()))
                return accessToken;

            if (string.IsNullOrEmpty(refreshToken))
            {
                tickets.Invalidate(sessionId);
                return null;
            }

            // Do not use the browser request's cancellation: another tab may need this single-use token.
            // HttpClient's timeout bounds the request. A lost response makes the session unusable.
            try
            {
                using var client = CreateClient(ticket);
                using var response = await client.PostAsJsonAsync("api/auth/refresh", new RefreshTokenRequest(refreshToken));
                if (response.IsSuccessStatusCode)
                {
                    var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
                    if (login is not null && !string.IsNullOrWhiteSpace(login.AccessToken)
                        && !string.IsNullOrWhiteSpace(login.RefreshToken) && login.ExpiresAtUtc > clock.GetUtcNow()
                        && login.RefreshTokenExpiresAtUtc is not null
                        && ticket.Properties.Items.TryGetValue(ServerTicketStore.RefreshTokenExpiryProperty, out string? refreshExpiry)
                        && DateTimeOffset.TryParse(refreshExpiry, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var originalExpiry)
                        // PostgreSQL persists timestamps with microsecond precision; .NET also has 100 ns ticks.
                        && login.RefreshTokenExpiresAtUtc.Value.UtcTicks / TimeSpan.TicksPerMicrosecond
                            == originalExpiry.UtcTicks / TimeSpan.TicksPerMicrosecond
                        && login.User is not null
                        && login.User.UserId.ToString() == ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier))
                    {
                        var principal = CreatePrincipal(login.User, ticket.AuthenticationScheme, sessionId);
                        ticket = new AuthenticationTicket(principal, ticket.Properties, ticket.AuthenticationScheme);
                        ticket.Properties.Items[ServerTicketStore.BackendTokenProperty] = login.AccessToken;
                        ticket.Properties.Items[ServerTicketStore.RefreshTokenProperty] = login.RefreshToken;
                        ticket.Properties.Items[ServerTicketStore.AccessTokenExpiryProperty] = login.ExpiresAtUtc.ToString("O");
                        if (tickets.TryUpdate(sessionId, session, ticket)) return login.AccessToken;
                    }
                }
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
            {
                logger.LogWarning("Backend session refresh failed ({FailureType}).", exception.GetType().Name);
            }
            tickets.Invalidate(sessionId);
            return null;
        }
        finally
        {
            session.Gate.Release();
        }
    }

    public async Task LogoutAsync(string sessionId)
    {
        ServerTicketStore.Session? session = tickets.GetSession(sessionId);
        if (session is null) return;
        await session.Gate.WaitAsync();
        try
        {
            AuthenticationTicket? ticket = tickets.GetValidTicket(sessionId);
            // Remove locally before contacting the API, even if revocation fails or times out.
            tickets.Invalidate(sessionId);
            if (ticket is null || !ticket.Properties.Items.TryGetValue(ServerTicketStore.RefreshTokenProperty, out string? refreshToken)
                || string.IsNullOrEmpty(refreshToken)) return;
            try
            {
                using var client = CreateClient(ticket);
                using var response = await client.PostAsJsonAsync("api/auth/logout", new RefreshTokenRequest(refreshToken));
                if (!response.IsSuccessStatusCode)
                    logger.LogWarning("Backend session revocation returned {StatusCode}.", (int)response.StatusCode);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                logger.LogWarning("Backend session revocation failed ({FailureType}).", exception.GetType().Name);
            }
        }
        finally
        {
            session.Gate.Release();
        }
    }

    internal static ClaimsPrincipal CreatePrincipal(AuthenticatedUserResponse user, string scheme, string? sessionId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()), new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Email)
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(user.Permissions.Select(permission => new Claim("permission", permission)));
        if (sessionId is not null) claims.Add(new Claim(ServerTicketStore.SessionIdClaim, sessionId));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, scheme));
    }

    internal static void ForwardClientIp(HttpClient client, string? address)
    {
        // Only forward the address established by the server, never a browser-supplied header.
        if (IPAddress.TryParse(address, out var ip))
            client.DefaultRequestHeaders.Add("X-Forwarded-For", (ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip).ToString());
    }

    private HttpClient CreateClient(AuthenticationTicket ticket)
    {
        var client = clients.CreateClient("BackendAuthentication");
        ticket.Properties.Items.TryGetValue(ServerTicketStore.ClientIpProperty, out string? address);
        ForwardClientIp(client, address);
        return client;
    }
}
