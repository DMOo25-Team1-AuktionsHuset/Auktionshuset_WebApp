using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Application.Abstraction.Auth;
using System.Security.Cryptography;

namespace Auktionshuset.Infrastructure.Service.Auth
{
    public sealed class DeviceSecretService : IDeviceSecretService
    {
        private const int SecretByteLength = 32;
        private const int SecretTextLength = SecretByteLength * 2;
        private const int HashTextLength = 64;

        public string GenerateSecret()
        {
            byte[] randomBytes =
                RandomNumberGenerator.GetBytes(SecretByteLength);

            return Convert.ToHexString(randomBytes);
        }

        public string HashSecret(string secret)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(secret);

            if (secret.Length != SecretTextLength)
            {
                throw new ArgumentException(
                    "Device secret must contain 64 characters.",
                    nameof(secret));
            }

            byte[] hash = ComputeHash(secret);

            return Convert.ToHexString(hash);
        }

        public bool VerifySecret(
            string? secret,
            string? storedHash)

        {
            if (
                string.IsNullOrWhiteSpace(secret) ||
                secret.Length != SecretTextLength ||
                string.IsNullOrWhiteSpace(storedHash) ||
                storedHash.Length != HashTextLength
                )
            {
                return false;
            }

            try
            {
                byte[] expectedHash =
                    Convert.FromHexString(storedHash);
                byte[] actualHash =
                    ComputeHash(secret);

                return CryptographicOperations.FixedTimeEquals(
                    actualHash,
                    expectedHash);
            }

            catch (FormatException)

            {
                return false;
            }
        }

        private static byte[] ComputeHash(string secret)
        {
            byte[] secretBytes =
                Encoding.UTF8.GetBytes(secret);

            return SHA256.HashData(secretBytes);

        }
    }
}
