using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Operia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileStandaloneBookingSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                EXEC(N'CREATE OR ALTER PROCEDURE [dbo].[AcquireBookingMutationLock]
                    @Resource nvarchar(255)
                AS
                BEGIN
                    SET NOCOUNT ON;
                    DECLARE @result int;
                    EXEC @result = sys.sp_getapplock
                        @Resource = @Resource,
                        @LockMode = N''Exclusive'',
                        @LockOwner = N''Transaction'',
                        @LockTimeout = 10000;
                    IF @result < 0
                        THROW 51000, ''Unable to acquire booking concurrency lock.'', 1;
                END')
                """);

            migrationBuilder.DropIndex(
                name: "IX_BookingPackageReservations_TenantId_BookingId",
                table: "BookingPackageReservations");

            migrationBuilder.DropIndex(
                name: "IX_BookingPackageReservations_TenantId_CustomerPackageId_SessionNumber",
                table: "BookingPackageReservations");

            migrationBuilder.CreateIndex(
                name: "IX_BookingPackageReservations_TenantId_BookingId",
                table: "BookingPackageReservations",
                columns: new[] { "TenantId", "BookingId" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingPackageReservations_TenantId_CustomerPackageId_SessionNumber",
                table: "BookingPackageReservations",
                columns: new[] { "TenantId", "CustomerPackageId", "SessionNumber" },
                unique: true,
                filter: "[ReleasedAtUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("EXEC(N'DROP PROCEDURE IF EXISTS [dbo].[AcquireBookingMutationLock]');");

            migrationBuilder.DropIndex(
                name: "IX_BookingPackageReservations_TenantId_BookingId",
                table: "BookingPackageReservations");

            migrationBuilder.DropIndex(
                name: "IX_BookingPackageReservations_TenantId_CustomerPackageId_SessionNumber",
                table: "BookingPackageReservations");

            migrationBuilder.CreateIndex(
                name: "IX_BookingPackageReservations_TenantId_BookingId",
                table: "BookingPackageReservations",
                columns: new[] { "TenantId", "BookingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingPackageReservations_TenantId_CustomerPackageId_SessionNumber",
                table: "BookingPackageReservations",
                columns: new[] { "TenantId", "CustomerPackageId", "SessionNumber" },
                unique: true);
        }
    }
}
