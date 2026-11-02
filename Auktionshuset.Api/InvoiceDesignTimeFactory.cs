using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Auktionshuset.Api;

// Schema generation needs neither running Vault/RabbitMQ nor production credentials.
public sealed class InvoiceDesignTimeFactory : IDesignTimeDbContextFactory<AHDBContext>
{
    public AHDBContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AHDBContext>()
        .UseNpgsql("Host=localhost;Database=design_only;Username=design_only").Options);
}
