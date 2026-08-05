using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PosFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomersTaxCurrencyAnd2FA : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TwoFactorEnabled",
                schema: "auth",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TwoFactorSecret",
                schema: "auth",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                schema: "pos",
                table: "Tenants",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "TaxRatePercent",
                schema: "pos",
                table: "Tenants",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "pos",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Customers",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LoyaltyPoints = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerId",
                schema: "pos",
                table: "Orders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_Phone",
                schema: "pos",
                table: "Customers",
                columns: new[] { "TenantId", "Phone" });

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Customers_CustomerId",
                schema: "pos",
                table: "Orders",
                column: "CustomerId",
                principalSchema: "pos",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Customers_CustomerId",
                schema: "pos",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "Customers",
                schema: "pos");

            migrationBuilder.DropIndex(
                name: "IX_Orders_CustomerId",
                schema: "pos",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TwoFactorEnabled",
                schema: "auth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TwoFactorSecret",
                schema: "auth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                schema: "pos",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TaxRatePercent",
                schema: "pos",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "pos",
                table: "Orders");
        }
    }
}
