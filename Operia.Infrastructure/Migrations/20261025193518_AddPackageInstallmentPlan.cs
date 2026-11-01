using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Operia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageInstallmentPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FirstPayment",
                table: "Packages",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InstallmentCount",
                table: "Packages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PackageInstallments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    PackageId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SessionsUnlocked = table.Column<int>(type: "int", nullable: false),
                    CumulativeAvailableSessions = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageInstallments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackageInstallments_Packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "Packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PackageInstallments_PackageId",
                table: "PackageInstallments",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageInstallments_TenantId_PackageId_Number",
                table: "PackageInstallments",
                columns: new[] { "TenantId", "PackageId", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackageInstallments");

            migrationBuilder.DropColumn(
                name: "FirstPayment",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "InstallmentCount",
                table: "Packages");
        }
    }
}
