using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Operia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeBusinessRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessId",
                table: "Employees",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE e
                SET e.BusinessId = b.Id
                FROM Employees e
                INNER JOIN Businesses b ON b.TenantId = e.TenantId
                WHERE e.BusinessId IS NULL
                """);

            migrationBuilder.AlterColumn<string>(
                name: "BusinessId",
                table: "Employees",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(36)",
                oldMaxLength: 36,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_BusinessId",
                table: "Employees",
                column: "BusinessId");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Businesses_BusinessId",
                table: "Employees",
                column: "BusinessId",
                principalTable: "Businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Businesses_BusinessId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_BusinessId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "Employees");
        }
    }
}
