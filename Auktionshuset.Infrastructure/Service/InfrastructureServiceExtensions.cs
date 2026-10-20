using Auktionshuset.Application.Abstraction.Admin.Employees;
using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.Abstraction.Auth;
using Auktionshuset.Infrastructure.Database;
using Auktionshuset.Infrastructure.Repositories;
using Auktionshuset.Infrastructure.Service.Lots;
using Auktionshuset.Infrastructure.Service.Vault;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using VaultSharp;
using VaultSharp.V1.AuthMethods.AppRole;

namespace Auktionshuset.Infrastructure.Service
{
    public static class InfrastructureServiceExtensions
    {
        /// <summary>
        /// Registers the infrastructure services, including messaging, repositories, and the
        /// PostgreSQL lot image store.
        /// </summary>
        /// <param name="services">The service collection to add the infrastructure services to.</param>
        /// <returns>The same service collection so that further calls can be chained.</returns>
        public static async Task<IServiceCollection> AddInfrastructureAsync(
            this IServiceCollection services,
            IConfiguration configuration)
        {

            // Vault configuration
            string vaultAddress = configuration["VAULT_ADDR"]
                                  ?? throw new InvalidOperationException("VAULT_ADDR mangler.");

            string roleId = configuration["VAULT_ROLE_ID"]
                            ?? throw new InvalidOperationException("VAULT_ROLE_ID mangler.");

            string secretId = configuration["VAULT_SECRET_ID"]
                              ?? throw new InvalidOperationException("VAULT_SECRET_ID mangler.");

            var authMethod = new AppRoleAuthMethodInfo(roleId, secretId);
            var vaultSettings = new VaultClientSettings(vaultAddress, authMethod);

            services.AddSingleton<IVaultClient>(
                _ => new VaultClient(vaultSettings));

            services.AddSingleton<VaultSecretProvider>();

            // Resolve Vault credentials during startup
            var vaultClient = new VaultClient(vaultSettings);
            var secretProvider = new VaultSecretProvider(vaultClient);

            DatabaseCredentials credentials =
                await secretProvider.GetDatabaseCredentialsAsync();

            RabbitMqCredentials rabbitMqCredentials =
                await secretProvider.GetRabbitMqCredentialsAsync();

            // Non-sensitive PostgreSQL configuration
            string host = configuration["Database:Host"]
                          ?? throw new InvalidOperationException("Database:Host mangler.");

            string database = configuration["Database:Name"]
                              ?? throw new InvalidOperationException("Database:Name mangler.");

            int port = configuration.GetValue<int?>("Database:Port") ?? 5432;

            var connectionStringBuilder = new NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = port,
                Database = database,
                Username = credentials.Username,
                Password = credentials.Password
            };

            string connectionString = connectionStringBuilder.ConnectionString;

            services.AddDbContext<AHDBContext>(options =>
                options.UseNpgsql(connectionString));

            // Existing infrastructure registrations
            services.AddRabbitMq(configuration, rabbitMqCredentials);
            services.AddScoped<ILotRepository, EFLotRepo>();
            services.AddScoped<IRefreshTokenStore, PostgresRefreshTokenStore>();
            services.AddSingleton<IEmployeeRepository, InMemoryEmployeeRepository>();
            services.AddScoped<ILotImageStore, PostgresLotImageStore>();

            return services;
        }
    }
}
