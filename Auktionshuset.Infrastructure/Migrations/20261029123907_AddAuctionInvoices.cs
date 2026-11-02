using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auktionshuset.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuctionInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AuctionNumber",
                table: "Auction",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuctionInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Number = table.Column<string>(type: "text", nullable: false),
                    AuctionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AuctionName = table.Column<string>(type: "text", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: false),
                    CustomerAddress = table.Column<string>(type: "text", nullable: false),
                    CustomerEmail = table.Column<string>(type: "text", nullable: false),
                    SellerName = table.Column<string>(type: "text", nullable: false),
                    SellerAddress = table.Column<string>(type: "text", nullable: false),
                    SellerEmail = table.Column<string>(type: "text", nullable: false),
                    SellerCvr = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    PaymentTerms = table.Column<string>(type: "text", nullable: false),
                    PremiumPercent = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    VatPercent = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    HammerTotal = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    PremiumTotal = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    VatTotal = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaidByName = table.Column<string>(type: "text", nullable: true),
                    PaymentMethod = table.Column<string>(type: "text", nullable: true),
                    PaidAmount = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuctionInvoices_Auction_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auction",
                        principalColumn: "AuctionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuctionInvoices_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BillingSequences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    LastInvoice = table.Column<long>(type: "bigint", nullable: false),
                    LastAuction = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingSequences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuctionInvoiceLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuctionLotId = table.Column<Guid>(type: "uuid", nullable: false),
                    WinningBidId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    HammerPrice = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    Premium = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    HammerVat = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    PremiumVat = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    WonAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CollectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CollectedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CollectedByName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionInvoiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuctionInvoiceLines_AuctionInvoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "AuctionInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuctionInvoiceLines_AuctionLot_AuctionLotId",
                        column: x => x.AuctionLotId,
                        principalTable: "AuctionLot",
                        principalColumn: "AuctionLotId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuctionInvoiceLines_Bids_WinningBidId",
                        column: x => x.WinningBidId,
                        principalTable: "Bids",
                        principalColumn: "BidId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auction_AuctionNumber",
                table: "Auction",
                column: "AuctionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuctionInvoiceLines_AuctionLotId",
                table: "AuctionInvoiceLines",
                column: "AuctionLotId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuctionInvoiceLines_InvoiceId",
                table: "AuctionInvoiceLines",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionInvoiceLines_WinningBidId",
                table: "AuctionInvoiceLines",
                column: "WinningBidId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuctionInvoices_AuctionId_CustomerId",
                table: "AuctionInvoices",
                columns: new[] { "AuctionId", "CustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuctionInvoices_CustomerId",
                table: "AuctionInvoices",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionInvoices_Number",
                table: "AuctionInvoices",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuctionInvoices_Sequence",
                table: "AuctionInvoices",
                column: "Sequence",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuctionInvoiceLines");

            migrationBuilder.DropTable(
                name: "BillingSequences");

            migrationBuilder.DropTable(
                name: "AuctionInvoices");

            migrationBuilder.DropIndex(
                name: "IX_Auction_AuctionNumber",
                table: "Auction");

            migrationBuilder.DropColumn(
                name: "AuctionNumber",
                table: "Auction");
        }
    }
}
