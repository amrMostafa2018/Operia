using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Operia.Infrastructure.Persistence;

#nullable disable

namespace Operia.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261028120000_AddCustomerRefundAndBlock")]
public partial class AddCustomerRefundAndBlock : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("IsBlocked", "Customers", type: "bit",
            nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>("BlockReason", "Customers", type: "nvarchar(500)",
            maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<byte[]>("Version", "Customers", type: "rowversion",
            rowVersion: true, nullable: false);

        migrationBuilder.CreateTable(
            name: "RefundTransactions",
            columns: table => new
            {
                Id = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                TenantId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                CustomerPackageId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                SaleId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                CustomerId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                IdempotencyKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                RecordedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RefundTransactions", x => x.Id);
                table.ForeignKey("FK_RefundTransactions_CustomerPackages_CustomerPackageId",
                    x => x.CustomerPackageId, "CustomerPackages", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_RefundTransactions_Customers_CustomerId",
                    x => x.CustomerId, "Customers", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_RefundTransactions_Sales_SaleId",
                    x => x.SaleId, "Sales", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_RefundTransactions_CustomerId", "RefundTransactions", "CustomerId");
        migrationBuilder.CreateIndex("IX_RefundTransactions_CustomerPackageId", "RefundTransactions", "CustomerPackageId");
        migrationBuilder.CreateIndex("IX_RefundTransactions_SaleId", "RefundTransactions", "SaleId");
        migrationBuilder.CreateIndex("IX_RefundTransactions_TenantId_CustomerPackageId_IdempotencyKey",
            "RefundTransactions", new[] { "TenantId", "CustomerPackageId", "IdempotencyKey" }, unique: true);
        migrationBuilder.CreateIndex("IX_RefundTransactions_TenantId_CustomerPackageId_RecordedAtUtc",
            "RefundTransactions", new[] { "TenantId", "CustomerPackageId", "RecordedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("RefundTransactions");
        migrationBuilder.DropColumn("IsBlocked", "Customers");
        migrationBuilder.DropColumn("BlockReason", "Customers");
        migrationBuilder.DropColumn("Version", "Customers");
    }
}
