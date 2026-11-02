using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auktionshuset.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeLogins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "SessionVersion", table: "RefreshTokens", type: "integer", nullable: false, defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerId",
                table: "LoginUsers",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                table: "LoginUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "LoginUsers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "SessionVersion",
                table: "LoginUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_LoginUsers_EmployeeId",
                table: "LoginUsers",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_LoginUser_Owner",
                table: "LoginUsers",
                sql: "(\"CustomerId\" IS NOT NULL AND \"EmployeeId\" IS NULL AND \"Role\" = 'Customer') OR (\"CustomerId\" IS NULL AND \"EmployeeId\" IS NOT NULL AND \"Role\" IN ('Admin', 'AuctionAdmin', 'LotAdmin', 'Employee'))");

            migrationBuilder.AddForeignKey(
                name: "FK_LoginUsers_Employee_EmployeeId",
                table: "LoginUsers",
                column: "EmployeeId",
                principalTable: "Employee",
                principalColumn: "EmployeeId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SessionVersion", table: "RefreshTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_LoginUsers_Employee_EmployeeId",
                table: "LoginUsers");

            migrationBuilder.DropIndex(
                name: "IX_LoginUsers_EmployeeId",
                table: "LoginUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LoginUser_Owner",
                table: "LoginUsers");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "LoginUsers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "LoginUsers");

            migrationBuilder.DropColumn(
                name: "SessionVersion",
                table: "LoginUsers");

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerId",
                table: "LoginUsers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
