using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Infrastructure.Seed
{
    internal static class EmployeeSeeder
    {
        private static readonly Employee[] Employees =
        [
            new Employee
            {
                EmployeeId = Guid.Parse("8c0f5b21-4d6e-4a90-b3c7-1e2f3a4b5c60"),
                AuctionHouseId = AHSeeder.DefaultAuctionHouseId,
                FirstName = "Mette",
                LastName = "Jørgensen",
                BirthDate = new DateOnly(1979, 4, 12),
                Address = "Strandgade 14, 6100 Haderslev"
            },
            new Employee
            {
                EmployeeId = Guid.Parse("3a7d9e04-5c81-4f2b-8d69-7b0c1a2e4f51"),
                AuctionHouseId = AHSeeder.DefaultAuctionHouseId,
                FirstName = "Henrik",
                LastName = "Sørensen",
                BirthDate = new DateOnly(1985, 11, 3),
                Address = "Nørregade 27, 6100 Haderslev"
            },
            new Employee
            {
                EmployeeId = Guid.Parse("c14b6f38-9e2a-4715-a8d0-5f3e2c7b9a02"),
                AuctionHouseId = AHSeeder.DefaultAuctionHouseId,
                FirstName = "Louise",
                LastName = "Bertelsen",
                BirthDate = new DateOnly(1992, 7, 21),
                Address = "Skolegade 8, 6100 Haderslev"
            },
            new Employee
            {
                EmployeeId = Guid.Parse("69b751ad-8f42-4ef8-95e0-a73c62814d30"),
                AuctionHouseId = AHSeeder.DefaultAuctionHouseId,
                FirstName = "Anders",
                LastName = "Mikkelsen",
                BirthDate = new DateOnly(1981, 2, 17),
                Address = "Havnevej 6, 6100 Haderslev"
            }
        ];

        public static async Task SeedAsync(
            AHDBContext context,
            CancellationToken cancellationToken = default)
        {
            var employeeIds = Employees.Select(employee => employee.EmployeeId).ToArray();
            var existingIds = await context.Employee
                .Where(employee => employeeIds.Contains(employee.EmployeeId))
                .Select(employee => employee.EmployeeId)
                .ToListAsync(cancellationToken);

            var existingIdSet = existingIds.ToHashSet();
            var missingEmployees = Employees
                .Where(employee => !existingIdSet.Contains(employee.EmployeeId))
                .ToArray();

            if (missingEmployees.Length == 0)
            {
                return;
            }

            context.Employee.AddRange(missingEmployees);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
