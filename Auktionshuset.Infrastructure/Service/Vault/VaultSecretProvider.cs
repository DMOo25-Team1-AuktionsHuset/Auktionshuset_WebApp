using VaultSharp;
using VaultSharp.V1.Commons;

namespace Auktionshuset.Infrastructure.Service.Vault
{
    public sealed class VaultSecretProvider(IVaultClient vaultClient)
    {

        public async Task<DatabaseCredentials> GetDatabaseCredentialsAsync(
            CancellationToken cancellationToken = default)
        {
            Secret<SecretData> secret = await vaultClient.V1.Secrets.KeyValue.V2
                .ReadSecretAsync(
                    path: "development/database",
                    mountPoint: "auction");

            IDictionary<string, object> data = secret.Data.Data;

            if (!data.TryGetValue("username", out object? username) ||
                !data.TryGetValue("password", out object? password))
            {
                throw new InvalidOperationException(
                    "Database credentials mangler i Vault.");
            }

            return new DatabaseCredentials(
                username?.ToString()
                ?? throw new InvalidOperationException("Username mangler."), password?.ToString()
                                                                             ?? throw new InvalidOperationException(
                                                                                 "Password mangler."));
        }

        public async Task<RabbitMqCredentials> GetRabbitMqCredentialsAsync()
        {
            Secret<SecretData> secret = await vaultClient.V1.Secrets.KeyValue.V2
                .ReadSecretAsync(
                    path: "development/rabbitmq",
                    mountPoint: "auction");

            IDictionary<string, object> data = secret.Data.Data;

            data.TryGetValue("username", out object? usernameValue);
            data.TryGetValue("password", out object? passwordValue);

            string? username = usernameValue?.ToString();
            string? password = passwordValue?.ToString();

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "RabbitMQ credentials mangler i Vault.");
            }

            return new RabbitMqCredentials(username, password);
        }
    }
}
