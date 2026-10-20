using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auktionshuset.Infrastructure.Migrations;

[DbContext(typeof(AHDBContext))]
[Migration("20261001120000_AllowUnassignedAuctioneer")]
public partial class AllowUnassignedAuctioneer : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<Guid>(
            name: "EmployeeId",
            table: "Auction",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE \"Auction\" AS auction SET \"EmployeeId\" = (SELECT \"EmployeeId\" FROM \"Employee\" ORDER BY \"EmployeeId\" LIMIT 1) WHERE \"EmployeeId\" IS NULL;");
        migrationBuilder.AlterColumn<Guid>(
            name: "EmployeeId",
            table: "Auction",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);
    }
}
