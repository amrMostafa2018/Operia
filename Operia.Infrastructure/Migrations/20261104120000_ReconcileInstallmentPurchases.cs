using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Operia.Infrastructure.Persistence;

namespace Operia.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261104120000_ReconcileInstallmentPurchases")]
public sealed class ReconcileInstallmentPurchases : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Before ReleasedSessions existed, Total was the number already released.
        // The saved schedule and purchase snapshot retain the full contract size.
        migrationBuilder.Sql("""
            UPDATE purchase
            SET ReleasedSessions = purchase.Total,
                Total = CASE WHEN contract.TotalSessions > purchase.Total
                    THEN contract.TotalSessions ELSE purchase.Total END
            FROM dbo.CustomerPackages AS purchase
            CROSS APPLY (
                SELECT COALESCE(
                    CASE WHEN ISJSON(purchase.PurchaseSnapshotJson) = 1
                        THEN TRY_CONVERT(int, JSON_VALUE(purchase.PurchaseSnapshotJson, '$.SessionCount'))
                    END,
                    (SELECT MAX(installment.CumulativeAvailableSessions)
                     FROM dbo.CustomerPackageInstallments AS installment
                     WHERE installment.TenantId = purchase.TenantId
                       AND installment.CustomerPackageId = purchase.Id),
                    purchase.Total) AS TotalSessions
            ) AS contract
            WHERE purchase.ReleasedSessions IS NULL
              AND EXISTS (
                  SELECT 1 FROM dbo.CustomerPackageInstallments AS installment
                  WHERE installment.TenantId = purchase.TenantId
                    AND installment.CustomerPackageId = purchase.Id);

            UPDATE installment
            SET CollectedAtUtc = collection.ConfirmedAtUtc,
                PaymentMethod = collection.PaymentMethod
            FROM dbo.CustomerPackageInstallments AS installment
            INNER JOIN dbo.InstallmentCollections AS collection
                ON collection.TenantId = installment.TenantId
               AND collection.CustomerPackageId = installment.CustomerPackageId
               AND collection.InstallmentNumber = installment.Number
            WHERE installment.CollectedAtUtc IS NULL;

            INSERT INTO dbo.InstallmentCollections
                (Id, TenantId, CustomerPackageId, InstallmentNumber, Amount,
                 PaymentMethod, IdempotencyKey, CollectedByUserId, ConfirmedAtUtc,
                 CreatedAt, CreatedBy, LastModifiedAt)
            SELECT CONVERT(nvarchar(36), NEWID()), installment.TenantId, installment.CustomerPackageId,
                   installment.Number, installment.Amount, COALESCE(installment.PaymentMethod, 'none'),
                   CONCAT('legacy:', installment.Id), '', installment.CollectedAtUtc,
                   installment.CollectedAtUtc, NULL, NULL
            FROM dbo.CustomerPackageInstallments AS installment
            INNER JOIN dbo.CustomerPackages AS purchase
                ON purchase.Id = installment.CustomerPackageId
               AND purchase.TenantId = installment.TenantId
            WHERE installment.CollectedAtUtc IS NOT NULL
              AND NOT EXISTS (
                  SELECT 1 FROM dbo.InstallmentCollections AS collection
                  WHERE collection.TenantId = installment.TenantId
                    AND collection.CustomerPackageId = installment.CustomerPackageId
                    AND collection.InstallmentNumber = installment.Number);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Historical payment and entitlement data must not be discarded.
    }
}
