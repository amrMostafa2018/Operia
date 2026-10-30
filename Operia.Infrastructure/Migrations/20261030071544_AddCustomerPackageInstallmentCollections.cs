using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Operia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerPackageInstallmentCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginBookingId",
                table: "CustomerPackages",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentOption",
                table: "BookingItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerPackageInstallments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    CustomerPackageId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CumulativeAvailableSessions = table.Column<int>(type: "int", nullable: false),
                    CollectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPackageInstallments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerPackageInstallments_CustomerPackages_CustomerPackageId",
                        column: x => x.CustomerPackageId,
                        principalTable: "CustomerPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPackages_TenantId_OriginBookingId",
                table: "CustomerPackages",
                columns: new[] { "TenantId", "OriginBookingId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPackageInstallments_CustomerPackageId",
                table: "CustomerPackageInstallments",
                column: "CustomerPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPackageInstallments_TenantId_CustomerPackageId_Number",
                table: "CustomerPackageInstallments",
                columns: new[] { "TenantId", "CustomerPackageId", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerPackageInstallments");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPackages_TenantId_OriginBookingId",
                table: "CustomerPackages");

            migrationBuilder.DropColumn(
                name: "OriginBookingId",
                table: "CustomerPackages");

            migrationBuilder.DropColumn(
                name: "PaymentOption",
                table: "BookingItems");
        }
    }
}
