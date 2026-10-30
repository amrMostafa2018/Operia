using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Operia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCashAndInstallmentPackagePrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CashEnabled",
                table: "Packages",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "InstallmentEnabled",
                table: "Packages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "InstallmentPrice",
                table: "Packages",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountDue",
                table: "CustomerPackages",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchasePrice",
                table: "CustomerPackages",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CashEnabled",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "InstallmentEnabled",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "InstallmentPrice",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "AmountDue",
                table: "CustomerPackages");

            migrationBuilder.DropColumn(
                name: "PurchasePrice",
                table: "CustomerPackages");
        }
    }
}
