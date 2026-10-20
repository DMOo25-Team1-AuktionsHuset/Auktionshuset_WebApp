namespace Auktionshuset.Security;

public sealed class FrontendRedirectState
{
    private string? location;

    public string? Location => Volatile.Read(ref location);

    public void RequestRedirect(string destination) =>
        Interlocked.CompareExchange(ref location, destination, null);
}
