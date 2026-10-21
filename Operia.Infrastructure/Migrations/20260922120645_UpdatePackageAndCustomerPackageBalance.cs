using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Operia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePackageAndCustomerPackageBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UsedSessions",
                table: "CustomerPackages",
                newName: "Used");

            migrationBuilder.RenameColumn(
                name: "TotalSessions",
                table: "CustomerPackages",
                newName: "Total");

            migrationBuilder.DropIndex(
                name: "IX_BookingPackageReservations_TenantId_CustomerPackageId_SessionNumber",
                table: "BookingPackageReservations");

            migrationBuilder.RenameColumn(
                name: "ReleasedAtUtc",
                table: "BookingPackageReservations",
                newName: "CancellationAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BookingPackageReservations_TenantId_CustomerPackageId_SessionNumber",
                table: "BookingPackageReservations",
                columns: new[] { "TenantId", "CustomerPackageId", "SessionNumber" },
                unique: true,
                filter: "[CancellationAtUtc] IS NULL");

            migrationBuilder.AlterColumn<int>(
                name: "SessionCount",
                table: "Packages",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "ReservedSessions",
                table: "CustomerPackages",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Used",
                table: "CustomerPackages",
                newName: "UsedSessions");

            migrationBuilder.RenameColumn(
                name: "Total",
                table: "CustomerPackages",
                newName: "TotalSessions");

            migrationBuilder.DropIndex(
                name: "IX_BookingPackageReservations_TenantId_CustomerPackageId_SessionNumber",
                table: "BookingPackageReservations");

            migrationBuilder.RenameColumn(
                name: "CancellationAtUtc",
                table: "BookingPackageReservations",
                newName: "ReleasedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BookingPackageReservations_TenantId_CustomerPackageId_SessionNumber",
                table: "BookingPackageReservations",
                columns: new[] { "TenantId", "CustomerPackageId", "SessionNumber" },
                unique: true,
                filter: "[ReleasedAtUtc] IS NULL");

            migrationBuilder.AlterColumn<int>(
                name: "SessionCount",
                table: "Packages",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ReservedSessions",
                table: "CustomerPackages",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
