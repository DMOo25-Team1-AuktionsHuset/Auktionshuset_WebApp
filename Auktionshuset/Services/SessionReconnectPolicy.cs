using Microsoft.AspNetCore.SignalR.Client;

namespace Auktionshuset.Services;

internal sealed class SessionReconnectPolicy(Func<bool> sessionIsValid) : IRetryPolicy
{
    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        if (!sessionIsValid() || retryContext.PreviousRetryCount >= 3)
        {
            return null;
        }

        return retryContext.PreviousRetryCount switch
        {
            0 => TimeSpan.Zero,
            1 => TimeSpan.FromSeconds(2),
            _ => TimeSpan.FromSeconds(10)
        };
    }
}
