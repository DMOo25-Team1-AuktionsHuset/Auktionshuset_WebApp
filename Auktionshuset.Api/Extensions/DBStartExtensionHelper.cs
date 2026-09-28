using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Api.Extensions;

public static class DBStartExtensionHelper
{
    /// <summary>
    /// Migrates the database to the latest version and seeds it with initial data.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task MigrateAndSeedDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AHDBContext>();

        await context.Database.MigrateAsync();
        await DBSeeder.SeedAsync(context);
    }
}
