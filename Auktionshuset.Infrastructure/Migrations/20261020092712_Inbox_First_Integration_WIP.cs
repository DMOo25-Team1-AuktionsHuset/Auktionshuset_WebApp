using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auktionshuset.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inbox_First_Integration_WIP : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InboxMessages",
                columns: table => new
                {
                    InboxId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransportMessageId = table.Column<string>(type: "text", nullable: true),
                    ConsumerName = table.Column<string>(type: "text", nullable: false),
                    MessageKey = table.Column<string>(type: "text", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<byte[]>(type: "bytea", nullable: false),
                    RecievedAtTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ProcessedAtTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    InboxQuarantinedAtTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => x.InboxId);
                });

            migrationBuilder.CreateIndex(
                name: "InboxMessages_Identity",
                table: "InboxMessages",
                columns: new[] { "ConsumerName", "MessageKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "InboxMessages_Ready",
                table: "InboxMessages",
                columns: new[] { "RecievedAtTime", "InboxId" },
                filter: "\"ProcessedAtTime\" IS NULL AND \"InboxQuarantinedAtTime\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboxMessages");
        }
    }
}
