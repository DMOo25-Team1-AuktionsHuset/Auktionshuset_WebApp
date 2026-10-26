using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.Abstraction.Auth
{
    public interface IDeviceSecretService
    {
        string GenerateSecret();
        string HashSecret(string secret);
        bool VerifySecret(string secret, string? storedHash);
    }
}
