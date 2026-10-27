using Auktionshuset.Api.Security;
using Auktionshuset.Api.Services;
using Auktionshuset.Application.Abstraction;
using Auktionshuset.Application.Abstraction.Admin.Auctions;
using Auktionshuset.Application.Abstraction.Admin.Employees;
using Auktionshuset.Application.Abstraction.Auth;
using Auktionshuset.Application.Admin.Auctions.CreateAuction;
using Auktionshuset.Application.Admin.Auctions.UpdateAuction;
using Auktionshuset.Infrastructure.Data;
using Auktionshuset.Infrastructure.Database;
//using Auktionshuset.Infrastructure.Messaging.InboxServices;
using Auktionshuset.Infrastructure.Repositories;
using Auktionshuset.Infrastructure.Service;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Auktionshuset.Api.Test;

public sealed class InfrastructureRegistrationTests
{
    [Fact]
    public async Task MergedServicesResolveDatabaseRepositoriesTransactionsAndMessaging()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=registration-test;Username=test;Password=test",
                ["RabbitMQ:HostName"] = "localhost",
                ["RabbitMQ:Port"] = "5672",
                ["RabbitMQ:UserName"] = "guest",
                ["RabbitMQ:Password"] = "guest"
            }).Build();
        IServiceCollection services = Host.CreateApplicationBuilder().Services;
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddSignalR();
        services.AddSecurityServices(configuration);
        services.AddApiServices();
        await services.AddInfrastructureAsync(configuration); ;
        await using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        provider.GetRequiredService<IStartupValidator>().Validate();
        Assert.NotNull(provider.GetRequiredService<HealthCheckService>());
        HealthCheckServiceOptions healthChecks = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        Assert.Contains(healthChecks.Registrations, check => check.Name == "database" && check.Tags.Contains("ready"));
        Assert.Contains(healthChecks.Registrations, check => check.Name == "messaging" && check.Tags.Contains("messaging"));

        Assert.IsType<EFAuctionRepository>(scope.ServiceProvider.GetRequiredService<IAuctionRepository>());
        Assert.IsType<EFEmployeeRepository>(scope.ServiceProvider.GetRequiredService<IEmployeeRepository>());
        Assert.IsType<EfUnitOfWork>(scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
        //       Assert.IsType<DbInboxStore>(scope.ServiceProvider.GetRequiredService<IInboxStore>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IOutboxWriter>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRefreshTokenStore>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CreateAuctionHandler>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<UpdateAuctionHandler>());
        IHostedService[] workers = provider.GetServices<IHostedService>().ToArray();
        Assert.Contains(workers, worker => worker.GetType().Name == "OutboxProcessor");
        Assert.Contains(workers, worker => worker.GetType().Name == "AdminEventsConsumer");

        AHDBContext context = scope.ServiceProvider.GetRequiredService<AHDBContext>();
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Equal("InboxMessages", context.Model.FindEntityType(typeof(InboxMessage))!.GetTableName());
        Assert.Equal("OutboxMessages", context.Model.FindEntityType(typeof(OutboxMessage))!.GetTableName());
    }
}
