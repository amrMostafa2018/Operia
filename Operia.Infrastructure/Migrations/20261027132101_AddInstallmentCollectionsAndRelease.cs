using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Operia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInstallmentCollectionsAndRelease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReleasedSessions",
                table: "CustomerPackages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InstallmentCollections",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    CustomerPackageId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    InstallmentNumber = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CollectedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstallmentCollections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstallmentCollections_CustomerPackages_CustomerPackageId",
                        column: x => x.CustomerPackageId,
                        principalTable: "CustomerPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstallmentCollections_CustomerPackageId",
                table: "InstallmentCollections",
                column: "CustomerPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_InstallmentCollections_TenantId_CustomerPackageId_IdempotencyKey",
                table: "InstallmentCollections",
                columns: new[] { "TenantId", "CustomerPackageId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstallmentCollections_TenantId_CustomerPackageId_InstallmentNumber",
                table: "InstallmentCollections",
                columns: new[] { "TenantId", "CustomerPackageId", "InstallmentNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstallmentCollections");

            migrationBuilder.DropColumn(
                name: "ReleasedSessions",
                table: "CustomerPackages");
        }
    }
}
