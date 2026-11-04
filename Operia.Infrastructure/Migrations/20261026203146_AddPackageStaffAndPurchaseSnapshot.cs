using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Operia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageStaffAndPurchaseSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllStaff",
                table: "Packages",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseSnapshotJson",
                table: "CustomerPackages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PackageStaff",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    PackageId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    EmployeeId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageStaff", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackageStaff_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackageStaff_Packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "Packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PackageStaff_EmployeeId",
                table: "PackageStaff",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageStaff_PackageId",
                table: "PackageStaff",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageStaff_TenantId_PackageId_EmployeeId",
                table: "PackageStaff",
                columns: new[] { "TenantId", "PackageId", "EmployeeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackageStaff");

            migrationBuilder.DropColumn(
                name: "AllStaff",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "PurchaseSnapshotJson",
                table: "CustomerPackages");
        }
    }
}
