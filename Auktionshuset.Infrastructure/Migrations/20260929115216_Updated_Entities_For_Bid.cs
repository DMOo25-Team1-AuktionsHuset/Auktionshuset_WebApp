using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auktionshuset.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Updated_Entities_For_Bid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuctionLot_Bid_CurrentHighestBidId",
                table: "AuctionLot");

            migrationBuilder.DropForeignKey(
                name: "FK_Bid_AuctionLot_AuctionLotId",
                table: "Bid");

            migrationBuilder.DropForeignKey(
                name: "FK_Bid_DeviceSession_DeviceSessionId",
                table: "Bid");

            migrationBuilder.DropForeignKey(
                name: "FK_DeviceSession_Auction_AuctionId",
                table: "DeviceSession");

            migrationBuilder.DropForeignKey(
                name: "FK_DeviceSession_Customer_CustomerId",
                table: "DeviceSession");

            migrationBuilder.DropForeignKey(
                name: "FK_DeviceSession_Device_DeviceId",
                table: "DeviceSession");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoice_Bid_WinningBidId",
                table: "Invoice");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoice_Customer_CustomerId",
                table: "Invoice");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DeviceSession",
                table: "DeviceSession");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Customer",
                table: "Customer");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Bid",
                table: "Bid");

            migrationBuilder.DropIndex(
                name: "IX_Bid_AuctionLotId",
                table: "Bid");

            migrationBuilder.RenameTable(
                name: "DeviceSession",
                newName: "DeviceSessions");

            migrationBuilder.RenameTable(
                name: "Customer",
                newName: "Customers");

            migrationBuilder.RenameTable(
                name: "Bid",
                newName: "Bids");

            migrationBuilder.RenameIndex(
                name: "IX_DeviceSession_DeviceId",
                table: "DeviceSessions",
                newName: "IX_DeviceSessions_DeviceId");

            migrationBuilder.RenameIndex(
                name: "IX_DeviceSession_CustomerId",
                table: "DeviceSessions",
                newName: "IX_DeviceSessions_CustomerId");

            migrationBuilder.RenameIndex(
                name: "IX_DeviceSession_AuctionId",
                table: "DeviceSessions",
                newName: "IX_DeviceSessions_AuctionId");

            migrationBuilder.RenameIndex(
                name: "IX_Bid_DeviceSessionId",
                table: "Bids",
                newName: "IX_Bids_DeviceSessionId");

            migrationBuilder.AddColumn<decimal>(
                name: "StartingPrice",
                table: "AuctionLot",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<Guid>(
                name: "DeviceSessionId",
                table: "Bids",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "Bids",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_DeviceSessions",
                table: "DeviceSessions",
                column: "DeviceSessionId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Customers",
                table: "Customers",
                column: "CustomerId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Bids",
                table: "Bids",
                column: "BidId");

            migrationBuilder.CreateTable(
                name: "BidCommandKeys",
                columns: table => new
                {
                    BidCommandKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuctionLotId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Accepted = table.Column<bool>(type: "boolean", nullable: false),
                    BidId = table.Column<Guid>(type: "uuid", nullable: true),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: true),
                    CurrentPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BidCommandKeys", x => x.BidCommandKeyId);
                    table.ForeignKey(
                        name: "FK_BidCommandKeys_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    OutboxId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    OccuredAtTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.OutboxId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bids_AuctionLotId_Amount_SequenceNumber",
                table: "Bids",
                columns: new[] { "AuctionLotId", "Amount", "SequenceNumber" },
                descending: new[] { false, true, false });

            migrationBuilder.CreateIndex(
                name: "IX_Bids_AuctionLotId_SequenceNumber",
                table: "Bids",
                columns: new[] { "AuctionLotId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bids_CustomerId",
                table: "Bids",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_BidCommandKeys_CustomerId_RequestId",
                table: "BidCommandKeys",
                columns: new[] { "CustomerId", "RequestId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AuctionLot_Bids_CurrentHighestBidId",
                table: "AuctionLot",
                column: "CurrentHighestBidId",
                principalTable: "Bids",
                principalColumn: "BidId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Bids_AuctionLot_AuctionLotId",
                table: "Bids",
                column: "AuctionLotId",
                principalTable: "AuctionLot",
                principalColumn: "AuctionLotId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bids_Customers_CustomerId",
                table: "Bids",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bids_DeviceSessions_DeviceSessionId",
                table: "Bids",
                column: "DeviceSessionId",
                principalTable: "DeviceSessions",
                principalColumn: "DeviceSessionId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceSessions_Auction_AuctionId",
                table: "DeviceSessions",
                column: "AuctionId",
                principalTable: "Auction",
                principalColumn: "AuctionId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceSessions_Customers_CustomerId",
                table: "DeviceSessions",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceSessions_Device_DeviceId",
                table: "DeviceSessions",
                column: "DeviceId",
                principalTable: "Device",
                principalColumn: "DeviceId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoice_Bids_WinningBidId",
                table: "Invoice",
                column: "WinningBidId",
                principalTable: "Bids",
                principalColumn: "BidId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoice_Customers_CustomerId",
                table: "Invoice",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuctionLot_Bids_CurrentHighestBidId",
                table: "AuctionLot");

            migrationBuilder.DropForeignKey(
                name: "FK_Bids_AuctionLot_AuctionLotId",
                table: "Bids");

            migrationBuilder.DropForeignKey(
                name: "FK_Bids_Customers_CustomerId",
                table: "Bids");

            migrationBuilder.DropForeignKey(
                name: "FK_Bids_DeviceSessions_DeviceSessionId",
                table: "Bids");

            migrationBuilder.DropForeignKey(
                name: "FK_DeviceSessions_Auction_AuctionId",
                table: "DeviceSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_DeviceSessions_Customers_CustomerId",
                table: "DeviceSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_DeviceSessions_Device_DeviceId",
                table: "DeviceSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoice_Bids_WinningBidId",
                table: "Invoice");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoice_Customers_CustomerId",
                table: "Invoice");

            migrationBuilder.DropTable(
                name: "BidCommandKeys");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DeviceSessions",
                table: "DeviceSessions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Customers",
                table: "Customers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Bids",
                table: "Bids");

            migrationBuilder.DropIndex(
                name: "IX_Bids_AuctionLotId_Amount_SequenceNumber",
                table: "Bids");

            migrationBuilder.DropIndex(
                name: "IX_Bids_AuctionLotId_SequenceNumber",
                table: "Bids");

            migrationBuilder.DropIndex(
                name: "IX_Bids_CustomerId",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "StartingPrice",
                table: "AuctionLot");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Bids");

            migrationBuilder.RenameTable(
                name: "DeviceSessions",
                newName: "DeviceSession");

            migrationBuilder.RenameTable(
                name: "Customers",
                newName: "Customer");

            migrationBuilder.RenameTable(
                name: "Bids",
                newName: "Bid");

            migrationBuilder.RenameIndex(
                name: "IX_DeviceSessions_DeviceId",
                table: "DeviceSession",
                newName: "IX_DeviceSession_DeviceId");

            migrationBuilder.RenameIndex(
                name: "IX_DeviceSessions_CustomerId",
                table: "DeviceSession",
                newName: "IX_DeviceSession_CustomerId");

            migrationBuilder.RenameIndex(
                name: "IX_DeviceSessions_AuctionId",
                table: "DeviceSession",
                newName: "IX_DeviceSession_AuctionId");

            migrationBuilder.RenameIndex(
                name: "IX_Bids_DeviceSessionId",
                table: "Bid",
                newName: "IX_Bid_DeviceSessionId");

            migrationBuilder.AlterColumn<Guid>(
                name: "DeviceSessionId",
                table: "Bid",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_DeviceSession",
                table: "DeviceSession",
                column: "DeviceSessionId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Customer",
                table: "Customer",
                column: "CustomerId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Bid",
                table: "Bid",
                column: "BidId");

            migrationBuilder.CreateIndex(
                name: "IX_Bid_AuctionLotId",
                table: "Bid",
                column: "AuctionLotId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuctionLot_Bid_CurrentHighestBidId",
                table: "AuctionLot",
                column: "CurrentHighestBidId",
                principalTable: "Bid",
                principalColumn: "BidId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Bid_AuctionLot_AuctionLotId",
                table: "Bid",
                column: "AuctionLotId",
                principalTable: "AuctionLot",
                principalColumn: "AuctionLotId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bid_DeviceSession_DeviceSessionId",
                table: "Bid",
                column: "DeviceSessionId",
                principalTable: "DeviceSession",
                principalColumn: "DeviceSessionId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceSession_Auction_AuctionId",
                table: "DeviceSession",
                column: "AuctionId",
                principalTable: "Auction",
                principalColumn: "AuctionId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceSession_Customer_CustomerId",
                table: "DeviceSession",
                column: "CustomerId",
                principalTable: "Customer",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceSession_Device_DeviceId",
                table: "DeviceSession",
                column: "DeviceId",
                principalTable: "Device",
                principalColumn: "DeviceId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoice_Bid_WinningBidId",
                table: "Invoice",
                column: "WinningBidId",
                principalTable: "Bid",
                principalColumn: "BidId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoice_Customer_CustomerId",
                table: "Invoice",
                column: "CustomerId",
                principalTable: "Customer",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
