using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Operia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeWorkingDayBranchId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM [EmployeeWorkingDays];");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeWorkingDays_TenantId_EmployeeId_Day",
                table: "EmployeeWorkingDays");

            migrationBuilder.AddColumn<string>(
                name: "BranchId",
                table: "EmployeeWorkingDays",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWorkingDays_BranchId",
                table: "EmployeeWorkingDays",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWorkingDays_TenantId_EmployeeId_BranchId_Day",
                table: "EmployeeWorkingDays",
                columns: new[] { "TenantId", "EmployeeId", "BranchId", "Day" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeWorkingDays_Branches_BranchId",
                table: "EmployeeWorkingDays",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeWorkingDays_Branches_BranchId",
                table: "EmployeeWorkingDays");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeWorkingDays_BranchId",
                table: "EmployeeWorkingDays");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeWorkingDays_TenantId_EmployeeId_BranchId_Day",
                table: "EmployeeWorkingDays");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "EmployeeWorkingDays");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWorkingDays_TenantId_EmployeeId_Day",
                table: "EmployeeWorkingDays",
                columns: new[] { "TenantId", "EmployeeId", "Day" },
                unique: true);
        }
    }
}
