using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;

namespace Auktionshuset.Security;

public sealed class BackendTokenAccessor(
    AuthenticationStateProvider authenticationStateProvider,
    IHttpContextAccessor httpContextAccessor,
    ServerTicketStore ticketStore)
{
    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        ClaimsPrincipal? user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            user = (await authenticationStateProvider.GetAuthenticationStateAsync()).User;
        }

        string? sessionId = user.FindFirstValue(ServerTicketStore.SessionIdClaim);
        if (user.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(sessionId))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        AuthenticationTicket? ticket = ticketStore.GetValidTicket(sessionId);
        if (ticket is null)
        {
            return null;
        }

        return ticket.Properties.Items.TryGetValue(ServerTicketStore.BackendTokenProperty, out string? token)
            ? token
            : null;
    }

    public async Task InvalidateCurrentSessionAsync()
    {
        ClaimsPrincipal? user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            user = (await authenticationStateProvider.GetAuthenticationStateAsync()).User;
        }

        string? sessionId = user.FindFirstValue(ServerTicketStore.SessionIdClaim);
        if (!string.IsNullOrEmpty(sessionId))
        {
            ticketStore.Invalidate(sessionId);
        }
    }
}
