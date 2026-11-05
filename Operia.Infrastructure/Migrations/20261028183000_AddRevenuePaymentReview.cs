using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Operia.Infrastructure.Persistence;

#nullable disable

namespace Operia.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261028183000_AddRevenuePaymentReview")]
public partial class AddRevenuePaymentReview : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("ReviewStatus", "SaleCollections", "nvarchar(20)",
            maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<byte[]>("ProofContent", "SaleCollections", "varbinary(max)",
            nullable: true);
        migrationBuilder.AddColumn<string>("ProofContentType", "SaleCollections", "nvarchar(50)",
            maxLength: 50, nullable: true);
        migrationBuilder.AddColumn<string>("ReviewedByUserId", "SaleCollections", "nvarchar(450)",
            maxLength: 450, nullable: true);
        migrationBuilder.AddColumn<DateTime>("ReviewedAtUtc", "SaleCollections", "datetime2",
            nullable: true);
        migrationBuilder.AddColumn<DateTime>("ConfirmedAtUtc", "SaleCollections", "datetime2",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("ReviewStatus", "SaleCollections");
        migrationBuilder.DropColumn("ProofContent", "SaleCollections");
        migrationBuilder.DropColumn("ProofContentType", "SaleCollections");
        migrationBuilder.DropColumn("ReviewedByUserId", "SaleCollections");
        migrationBuilder.DropColumn("ReviewedAtUtc", "SaleCollections");
        migrationBuilder.DropColumn("ConfirmedAtUtc", "SaleCollections");
    }
}
