using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Operia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleCollectionAndBookingLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Disposition",
                table: "Sales",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Later");

            migrationBuilder.AddColumn<string>(
                name: "SaleId",
                table: "CustomerPackages",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SaleItemIndex",
                table: "CustomerPackages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SaleUnitIndex",
                table: "CustomerPackages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SaleId",
                table: "Bookings",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SaleCollections",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    SaleId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Confirmed = table.Column<bool>(type: "bit", nullable: false),
                    AttemptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CollectedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleCollections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleCollections_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPackages_SaleId",
                table: "CustomerPackages",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPackages_TenantId_SaleId_SaleItemIndex_SaleUnitIndex",
                table: "CustomerPackages",
                columns: new[] { "TenantId", "SaleId", "SaleItemIndex", "SaleUnitIndex" },
                unique: true,
                filter: "[SaleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_SaleId",
                table: "Bookings",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TenantId_SaleId",
                table: "Bookings",
                columns: new[] { "TenantId", "SaleId" },
                unique: true,
                filter: "[SaleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SaleCollections_SaleId",
                table: "SaleCollections",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleCollections_TenantId_SaleId",
                table: "SaleCollections",
                columns: new[] { "TenantId", "SaleId" },
                unique: true,
                filter: "[Confirmed] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SaleCollections_TenantId_SaleId_IdempotencyKey",
                table: "SaleCollections",
                columns: new[] { "TenantId", "SaleId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Sales_SaleId",
                table: "Bookings",
                column: "SaleId",
                principalTable: "Sales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerPackages_Sales_SaleId",
                table: "CustomerPackages",
                column: "SaleId",
                principalTable: "Sales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Sales_SaleId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerPackages_Sales_SaleId",
                table: "CustomerPackages");

            migrationBuilder.DropTable(
                name: "SaleCollections");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPackages_SaleId",
                table: "CustomerPackages");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPackages_TenantId_SaleId_SaleItemIndex_SaleUnitIndex",
                table: "CustomerPackages");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_SaleId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_TenantId_SaleId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "Disposition",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "SaleId",
                table: "CustomerPackages");

            migrationBuilder.DropColumn(
                name: "SaleItemIndex",
                table: "CustomerPackages");

            migrationBuilder.DropColumn(
                name: "SaleUnitIndex",
                table: "CustomerPackages");

            migrationBuilder.DropColumn(
                name: "SaleId",
                table: "Bookings");
        }
    }
}
