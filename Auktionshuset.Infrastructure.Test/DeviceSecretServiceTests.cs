using Auktionshuset.Application.Abstraction.Auth;
using Auktionshuset.Infrastructure.Service.Auth;
using Xunit;

namespace Auktionshuset.Infrastructure.Test;

public sealed class DeviceSecretServiceTests
{
    private readonly IDeviceSecretService _service =
        new DeviceSecretService();

    // Offentlige testdata, ikke rigtige credentials.
    private static readonly string KnownSecret =
        new string('a', 64);

    // SHA-256 af teksten med 64 små a'er.
    private const string KnownHash =
        "FFE054FE7AE0CB6DC65C3AF9B61D5209F439851DB43D0BA5997337DF154668EB";

    [Fact]
    public void GenerateSecret_Returns32BytesEncodedAsHex()
    {
        string secret = _service.GenerateSecret();

        byte[] bytes = Convert.FromHexString(secret);

        Assert.Equal(64, secret.Length);
        Assert.Equal(32, bytes.Length);
    }

    [Fact]
    public void HashSecret_ReturnsExpectedHash()
    {
        string hash = _service.HashSecret(KnownSecret);

        Assert.Equal(KnownHash, hash);
    }

    [Fact]
    public void VerifySecret_AcceptsCorrectSecret()
    {
        bool accepted = _service.VerifySecret(
            KnownSecret,
            KnownHash);

        Assert.True(accepted);
    }

    [Fact]
    public void VerifySecret_RejectsIncorrectSecret()
    {
        string incorrectSecret = new string('b', 64);

        bool accepted = _service.VerifySecret(
            incorrectSecret,
            KnownHash);

        Assert.False(accepted);
    }

    [Fact]
    public void VerifySecret_IsCaseSensitive()
    {
        string uppercaseSecret = new string('A', 64);

        bool accepted = _service.VerifySecret(
            uppercaseSecret,
            KnownHash);

        Assert.False(accepted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("too-short")]
    public void VerifySecret_RejectsInvalidSecret(
        string? secret)
    {
        bool accepted = _service.VerifySecret(
            secret,
            KnownHash);

        Assert.False(accepted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-hash")]
    public void VerifySecret_RejectsInvalidStoredHash(
        string? storedHash)
    {
        bool accepted = _service.VerifySecret(
            KnownSecret,
            storedHash);

        Assert.False(accepted);
    }

    [Fact]
    public void VerifySecret_RejectsMalformedHashOfCorrectLength()
    {
        // Længden passer, men Z er ikke et gyldigt hex-tegn.
        string malformedHash = new string('Z', 64);

        bool accepted = _service.VerifySecret(
            KnownSecret,
            malformedHash);

        Assert.False(accepted);
    }
}
