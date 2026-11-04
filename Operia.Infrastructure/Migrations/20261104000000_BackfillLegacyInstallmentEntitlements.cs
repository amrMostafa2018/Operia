using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Operia.Infrastructure.Persistence;

namespace Operia.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261104000000_BackfillLegacyInstallmentEntitlements")]
public sealed class BackfillLegacyInstallmentEntitlements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Earlier installment purchases stored released sessions in Total. The
        // final purchase row retains the complete entitlement after offer edits.
        migrationBuilder.Sql("""
            UPDATE purchase
            SET ReleasedSessions = purchase.Total,
                Total = plan.TotalSessions
            FROM dbo.CustomerPackages AS purchase
            CROSS APPLY (
                SELECT MAX(installment.CumulativeAvailableSessions) AS TotalSessions
                FROM dbo.CustomerPackageInstallments AS installment
                WHERE installment.CustomerPackageId = purchase.Id
                  AND installment.TenantId = purchase.TenantId
            ) AS plan
            WHERE purchase.ReleasedSessions IS NULL
              AND plan.TotalSessions > 0
              AND JSON_VALUE(purchase.PurchaseSnapshotJson, '$.PaymentOption') = 'installment';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The old Total cannot be reconstructed after further collections.
    }
}
