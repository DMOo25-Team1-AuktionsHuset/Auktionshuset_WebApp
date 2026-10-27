using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auktionshuset.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MondayMorning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MessageReplayAudits",
                columns: table => new
                {
                    ReplayId = table.Column<Guid>(type: "uuid", nullable: false),
                    Pipeline = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RequestedAtTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PreviousAttempts = table.Column<int>(type: "integer", nullable: false),
                    PreviousError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageReplayAudits", x => x.ReplayId);
                });

            migrationBuilder.CreateIndex(
                name: "OutboxMessages_Quarantine",
                table: "OutboxMessages",
                columns: new[] { "QuarantinedAtTime", "OutboxId" },
                filter: "\"ProcessedAtTime\" IS NULL AND \"QuarantinedAtTime\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "OutboxMessages_Retention",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAtTime", "OutboxId" },
                filter: "\"ProcessedAtTime\" IS NOT NULL AND \"QuarantinedAtTime\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "InboxMessages_Quarantine",
                table: "InboxMessages",
                columns: new[] { "InboxQuarantinedAtTime", "InboxId" },
                filter: "\"ProcessedAtTime\" IS NULL AND \"InboxQuarantinedAtTime\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MessageReplayAudits_Pipeline_MessageId_RequestedAtTime",
                table: "MessageReplayAudits",
                columns: new[] { "Pipeline", "MessageId", "RequestedAtTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessageReplayAudits");

            migrationBuilder.DropIndex(
                name: "OutboxMessages_Quarantine",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "OutboxMessages_Retention",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "InboxMessages_Quarantine",
                table: "InboxMessages");
        }
    }
}
