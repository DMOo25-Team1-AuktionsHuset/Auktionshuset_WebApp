using Auktionshuset.Infrastructure.Seed;

namespace Auktionshuset.Infrastructure.Database
{
    public static class DBSeeder
    {
        public static async Task SeedAsync(
            AHDBContext context,
            CancellationToken cancellationToken = default)
        {
            await AHSeeder.SeedAsync(context, cancellationToken);
            await EmployeeSeeder.SeedAsync(context, cancellationToken);
            await LotSeeder.SeedAsync(context, cancellationToken);
        }
    }
}
