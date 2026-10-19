using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Server;

namespace Auktionshuset.Security;

public sealed class SessionRevalidatingAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    ServerTicketStore ticketStore,
    BackendSessionService sessions)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromSeconds(30);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState,
        CancellationToken cancellationToken)
    {
        ClaimsPrincipal user = authenticationState.User;
        string? sessionId = user.FindFirstValue(ServerTicketStore.SessionIdClaim);
        if (user.Identity?.IsAuthenticated != true) return true;
        if (sessionId is null || await sessions.GetAccessTokenAsync(sessionId, cancellationToken) is null) return false;
        AuthenticationTicket? ticket = ticketStore.GetValidTicket(sessionId);
        if (ticket is null) return false;
        if (!user.Claims.Select(claim => (claim.Type, claim.Value))
            .SequenceEqual(ticket.Principal.Claims.Select(claim => (claim.Type, claim.Value))))
            SetAuthenticationState(Task.FromResult(new AuthenticationState(ticket.Principal)));
        return true;
    }
}
