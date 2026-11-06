using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Operia.Infrastructure.Persistence;

#nullable disable

namespace Operia.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261106120000_AddPurchaseBranchProvenance")]
public partial class AddPurchaseBranchProvenance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("BranchId", "CustomerPackages", "nvarchar(36)",
            maxLength: 36, nullable: true);
        migrationBuilder.AddColumn<string>("BranchAttributionSource", "CustomerPackages", "nvarchar(40)",
            maxLength: 40, nullable: true);
        migrationBuilder.CreateIndex("IX_CustomerPackages_TenantId_BranchId", "CustomerPackages",
            ["TenantId", "BranchId"]);

        migrationBuilder.Sql("""
            SELECT p.Id AS PurchaseId, p.TenantId, evidence.BranchId,
                   MIN(evidence.Priority) AS Priority
            INTO #PurchaseBranchEvidence
            FROM CustomerPackages p
            CROSS APPLY (
                SELECT s.BranchId, 1 AS Priority
                FROM Sales s
                WHERE s.Id = p.SaleId AND s.TenantId = p.TenantId
                  AND s.CustomerId = p.CustomerId
                UNION ALL
                SELECT b.BranchId, 2
                FROM Bookings b
                WHERE b.Id = p.OriginBookingId AND b.TenantId = p.TenantId
                  AND b.CustomerId = p.CustomerId
                UNION ALL
                SELECT b.BranchId, 3
                FROM BookingPackageReservations r
                JOIN Bookings b ON b.Id = r.BookingId AND b.TenantId = r.TenantId
                WHERE r.CustomerPackageId = p.Id AND r.TenantId = p.TenantId
                  AND b.CustomerId = p.CustomerId
            ) evidence
            JOIN Branches branch ON branch.Id = evidence.BranchId
                AND branch.TenantId = p.TenantId
            WHERE p.BranchId IS NULL
            GROUP BY p.Id, p.TenantId, evidence.BranchId;

            SELECT PurchaseId, TenantId, MIN(BranchId) AS BranchId,
                   MIN(Priority) AS Priority
            INTO #ProvenPurchaseBranches
            FROM #PurchaseBranchEvidence
            GROUP BY PurchaseId, TenantId
            HAVING COUNT(DISTINCT BranchId) = 1;

            UPDATE p SET BranchId = proven.BranchId,
                BranchAttributionSource = CASE proven.Priority
                    WHEN 1 THEN 'Sale' WHEN 2 THEN 'OriginBooking' ELSE 'Reservation' END
            FROM CustomerPackages p
            JOIN #ProvenPurchaseBranches proven ON proven.PurchaseId = p.Id
                AND proven.TenantId = p.TenantId;

            INSERT INTO AuditLogs (Id, TenantId, Action, EntityType, EntityId, EntityName,
                                   DetailsJson, CreatedAt)
            SELECT CONVERT(nvarchar(36), NEWID()), p.TenantId, 'PurchaseBranchAttributed',
                   'CustomerPackage', p.Id, p.Id,
                   (SELECT CAST(NULL AS nvarchar(36)) AS oldValue,
                           p.BranchId AS newValue,
                           p.BranchAttributionSource AS evidence
                    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES), SYSUTCDATETIME()
            FROM CustomerPackages p
            JOIN #ProvenPurchaseBranches proven ON proven.PurchaseId = p.Id
                AND proven.TenantId = p.TenantId;

            DROP TABLE #ProvenPurchaseBranches;
            DROP TABLE #PurchaseBranchEvidence;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_CustomerPackages_TenantId_BranchId", "CustomerPackages");
        migrationBuilder.DropColumn("BranchAttributionSource", "CustomerPackages");
        migrationBuilder.DropColumn("BranchId", "CustomerPackages");
    }
}
