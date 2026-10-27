using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auktionshuset.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inbox_Processor_Policy_RabbitMQ : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "InboxMessages_Ready",
                table: "InboxMessages");

            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "InboxMessages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseExpiresAtTime",
                table: "InboxMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LeaseToken",
                table: "InboxMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAtTime",
                table: "InboxMessages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.CreateIndex(
                name: "InboxMessages_Ready",
                table: "InboxMessages",
                columns: new[] { "ConsumerName", "NextAttemptAtTime", "RecievedAtTime", "InboxId" },
                filter: "\"ProcessedAtTime\" IS NULL AND \"InboxQuarantinedAtTime\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "InboxMessages_Ready",
                table: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAtTime",
                table: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "LeaseToken",
                table: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "NextAttemptAtTime",
                table: "InboxMessages");

            migrationBuilder.CreateIndex(
                name: "InboxMessages_Ready",
                table: "InboxMessages",
                columns: new[] { "RecievedAtTime", "InboxId" },
                filter: "\"ProcessedAtTime\" IS NULL AND \"InboxQuarantinedAtTime\" IS NULL");
        }
    }
}
