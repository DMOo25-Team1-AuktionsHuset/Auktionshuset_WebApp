using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auktionshuset.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PersistDeviceSessionRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SessionRevision", table: "DeviceCredentials", type: "bigint", nullable: false, defaultValue: 0L);
            migrationBuilder.AddColumn<long>(
                name: "ResultAuctionRevision", table: "BidCommandKeys", type: "bigint", nullable: true);

            // Existing session history cannot be reconstructed, so establish one baseline.
            migrationBuilder.Sql("""
                UPDATE "DeviceCredentials" SET "SessionRevision" = 1
                WHERE "SessionRevision" = 0 AND EXISTS (
                    SELECT 1 FROM "DeviceSessions" WHERE "DeviceSessions"."DeviceId" = "DeviceCredentials"."DeviceId"
                )
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SessionRevision", table: "DeviceCredentials");
            migrationBuilder.DropColumn(name: "ResultAuctionRevision", table: "BidCommandKeys");
        }
    }
}
