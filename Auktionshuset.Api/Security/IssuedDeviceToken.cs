namespace Auktionshuset.Api.Security
{
    public sealed class IssuedDeviceToken
    {
        public required string AccessToken { get; init; }

        public required DateTimeOffset ExpiresAtUtc { get; init; }
    }
}
