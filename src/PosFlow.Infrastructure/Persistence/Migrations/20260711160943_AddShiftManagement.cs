using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PosFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CashDifference",
                schema: "pos",
                table: "Shifts",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CashSales",
                schema: "pos",
                table: "Shifts",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedCash",
                schema: "pos",
                table: "Shifts",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_BranchId",
                schema: "pos",
                table: "Shifts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_TenantId_BranchId_UserId",
                schema: "pos",
                table: "Shifts",
                columns: new[] { "TenantId", "BranchId", "UserId" },
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Shifts_Branches_BranchId",
                schema: "pos",
                table: "Shifts",
                column: "BranchId",
                principalSchema: "pos",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Shifts_Tenants_TenantId",
                schema: "pos",
                table: "Shifts",
                column: "TenantId",
                principalSchema: "pos",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shifts_Branches_BranchId",
                schema: "pos",
                table: "Shifts");

            migrationBuilder.DropForeignKey(
                name: "FK_Shifts_Tenants_TenantId",
                schema: "pos",
                table: "Shifts");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_BranchId",
                schema: "pos",
                table: "Shifts");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_TenantId_BranchId_UserId",
                schema: "pos",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "CashDifference",
                schema: "pos",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "CashSales",
                schema: "pos",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "ExpectedCash",
                schema: "pos",
                table: "Shifts");
        }
    }
}
