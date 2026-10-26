using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auktionshuset.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class phase_9 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAtTime",
                table: "AuctionLot",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAtTime",
                table: "Auction",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAtTime",
                table: "Auction",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClosedAtTime",
                table: "AuctionLot");

            migrationBuilder.DropColumn(
                name: "ClosedAtTime",
                table: "Auction");

            migrationBuilder.DropColumn(
                name: "StartedAtTime",
                table: "Auction");
        }
    }
}
