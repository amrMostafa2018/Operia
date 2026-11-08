using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Operia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PersistPurchaseStartAndVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "CustomerPackages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTestRecord",
                table: "CustomerPackages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartedOn",
                table: "CustomerPackages",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "CustomerPackages");

            migrationBuilder.DropColumn(
                name: "IsTestRecord",
                table: "CustomerPackages");

            migrationBuilder.DropColumn(
                name: "StartedOn",
                table: "CustomerPackages");
        }
    }
}
