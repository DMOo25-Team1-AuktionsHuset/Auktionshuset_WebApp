using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace Auktionshuset.Security;

public sealed class SessionRevalidatingAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    ServerTicketStore ticketStore)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromSeconds(30);

    protected override Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState,
        CancellationToken cancellationToken)
    {
        ClaimsPrincipal user = authenticationState.User;
        string? sessionId = user.FindFirstValue(ServerTicketStore.SessionIdClaim);
        return Task.FromResult(
            user.Identity?.IsAuthenticated != true
            || (sessionId is not null && ticketStore.IsValid(sessionId)));
    }
}
