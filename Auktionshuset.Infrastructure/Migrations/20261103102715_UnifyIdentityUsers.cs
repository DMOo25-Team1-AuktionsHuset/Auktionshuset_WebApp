using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auktionshuset.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UnifyIdentityUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fail atomically on ambiguous identities; never discard or silently merge accounts.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "AspNetUsers" WHERE "Email" IS NULL OR trim("Email") = '' OR "PasswordHash" IS NULL) THEN
                        RAISE EXCEPTION 'Reconcile Identity accounts without an email/password before UnifyIdentityUsers.';
                    END IF;
                    IF EXISTS (
                        SELECT upper(trim(email)) FROM (
                            SELECT "Email" AS email FROM "AspNetUsers"
                            UNION ALL SELECT "Email" FROM "LoginUsers"
                        ) accounts GROUP BY upper(trim(email)) HAVING count(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Reconcile duplicate emails across AspNetUsers and LoginUsers before UnifyIdentityUsers.';
                    END IF;
                END; $$;
                """);

            migrationBuilder.DropIndex(
                name: "EmailIndex",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "AspNetUsers",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "AspNetUsers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemAccount",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "AspNetUsers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SessionVersion",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("""
                UPDATE "AspNetUsers" AS u SET
                    "UserId" = md5('identity:' || u."Id")::uuid,
                    "Email" = lower(trim(u."Email")),
                    "NormalizedEmail" = upper(trim(u."Email")),
                    "UserName" = COALESCE(u."UserName", lower(trim(u."Email"))),
                    "NormalizedUserName" = upper(COALESCE(u."UserName", trim(u."Email"))),
                    "IsSystemAccount" = TRUE,
                    "Role" = COALESCE((SELECT r."Name" FROM "AspNetUserRoles" ur
                        JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
                        WHERE ur."UserId" = u."Id" AND r."Name" IN ('Admin', 'AuctionAdmin', 'LotAdmin', 'Employee', 'Customer')
                        ORDER BY CASE r."Name" WHEN 'Admin' THEN 0 WHEN 'AuctionAdmin' THEN 1 WHEN 'LotAdmin' THEN 2 WHEN 'Employee' THEN 3 ELSE 4 END
                        LIMIT 1), 'Customer'),
                    "IsActive" = EXISTS (SELECT 1 FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
                        WHERE ur."UserId" = u."Id" AND r."Name" IN ('Admin', 'AuctionAdmin', 'LotAdmin', 'Employee', 'Customer'));

                INSERT INTO "AspNetUsers" ("Id", "UserId", "Email", "NormalizedEmail", "UserName", "NormalizedUserName",
                    "PasswordHash", "Role", "CustomerId", "EmployeeId", "IsActive", "SessionVersion", "IsSystemAccount",
                    "SecurityStamp", "ConcurrencyStamp", "EmailConfirmed", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
                SELECT "UserId"::text, "UserId", lower(trim("Email")), upper(trim("Email")), lower(trim("Email")), upper(trim("Email")),
                    "PasswordHash", "Role", "CustomerId", "EmployeeId", "IsActive", "SessionVersion", FALSE,
                    md5(random()::text), md5(random()::text), FALSE, FALSE, FALSE, TRUE, 0
                FROM "LoginUsers";
                """);

            migrationBuilder.DropTable(name: "LoginUsers");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CustomerId",
                table: "AspNetUsers",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_Email",
                table: "AspNetUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_EmployeeId",
                table: "AspNetUsers",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_UserId",
                table: "AspNetUsers",
                column: "UserId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_LoginUser_Owner",
                table: "AspNetUsers",
                sql: "(\"IsSystemAccount\" AND \"CustomerId\" IS NULL AND \"EmployeeId\" IS NULL) OR (NOT \"IsSystemAccount\" AND ((\"CustomerId\" IS NOT NULL AND \"EmployeeId\" IS NULL AND \"Role\" = 'Customer') OR (\"CustomerId\" IS NULL AND \"EmployeeId\" IS NOT NULL AND \"Role\" IN ('Admin', 'AuctionAdmin', 'LotAdmin', 'Employee'))))");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Customers_CustomerId",
                table: "AspNetUsers",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Employee_EmployeeId",
                table: "AspNetUsers",
                column: "EmployeeId",
                principalTable: "Employee",
                principalColumn: "EmployeeId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Identity account consolidation cannot be downgraded without losing account data. Restore a database backup instead.");
        }
    }
}
