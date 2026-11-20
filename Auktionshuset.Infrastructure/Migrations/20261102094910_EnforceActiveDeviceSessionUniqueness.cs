using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auktionshuset.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceActiveDeviceSessionUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DeviceSessions_Active_CustomerId_AuctionId",
                table: "DeviceSessions",
                columns: new[] { "CustomerId", "AuctionId" },
                unique: true,
                filter: "\"EndedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSessions_Active_DeviceId",
                table: "DeviceSessions",
                column: "DeviceId",
                unique: true,
                filter: "\"EndedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeviceSessions_Active_CustomerId_AuctionId",
                table: "DeviceSessions");

            migrationBuilder.DropIndex(
                name: "IX_DeviceSessions_Active_DeviceId",
                table: "DeviceSessions");
        }
    }
}
