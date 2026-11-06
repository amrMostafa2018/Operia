-- Run the report after AddPurchaseBranchProvenance. A null branch is deliberately excluded
-- from branch-scoped customer and revenue views until documentary evidence is reviewed.
SELECT p.TenantId, p.Id AS PurchaseId, p.CustomerId, p.SaleId,
       p.OriginBookingId, COUNT(DISTINCT b.BranchId) AS ReservationBranchCount,
       STRING_AGG(CONVERT(nvarchar(max), b.BranchId), ',') AS ReservationBranchEvidence
FROM CustomerPackages p
LEFT JOIN BookingPackageReservations r ON r.TenantId = p.TenantId
    AND r.CustomerPackageId = p.Id
LEFT JOIN Bookings b ON b.Id = r.BookingId AND b.TenantId = p.TenantId
    AND b.CustomerId = p.CustomerId
WHERE p.BranchId IS NULL
GROUP BY p.TenantId, p.Id, p.CustomerId, p.SaleId, p.OriginBookingId
ORDER BY p.TenantId, p.Id;

-- For a verified external source, fill all five parameters and execute this block once
-- per purchase as a database administrator. Preserve the document reference for review.
-- A booking or sale that contradicts the proposed branch causes the transaction to fail.
SET XACT_ABORT ON;
DECLARE @TenantId nvarchar(36) = NULL;
DECLARE @PurchaseId nvarchar(36) = NULL;
DECLARE @BranchId nvarchar(36) = NULL;
DECLARE @EvidenceReference nvarchar(200) = NULL;
DECLARE @ActorUserId nvarchar(450) = NULL;

IF @TenantId IS NULL OR @PurchaseId IS NULL OR @BranchId IS NULL OR
   NULLIF(LTRIM(RTRIM(@EvidenceReference)), '') IS NULL OR
   NULLIF(LTRIM(RTRIM(@ActorUserId)), '') IS NULL
    THROW 51000, 'Tenant, purchase, branch, evidence reference, and actor are required.', 1;

BEGIN TRANSACTION;
IF NOT EXISTS (SELECT 1 FROM Branches WHERE Id = @BranchId AND TenantId = @TenantId)
    THROW 51001, 'Branch does not belong to the tenant.', 1;
IF NOT EXISTS (SELECT 1 FROM AspNetUsers WHERE Id = @ActorUserId AND TenantId = @TenantId)
    THROW 51004, 'Actor does not belong to the tenant.', 1;
IF NOT EXISTS (SELECT 1 FROM CustomerPackages WITH (UPDLOCK, HOLDLOCK)
               WHERE Id = @PurchaseId AND TenantId = @TenantId AND BranchId IS NULL)
    THROW 51002, 'Purchase is missing or already attributed.', 1;
IF EXISTS (
    SELECT 1 FROM CustomerPackages p
    JOIN Sales s ON s.Id = p.SaleId AND s.TenantId = p.TenantId
    WHERE p.Id = @PurchaseId AND p.TenantId = @TenantId AND s.BranchId <> @BranchId
    UNION ALL
    SELECT 1 FROM CustomerPackages p
    JOIN Bookings b ON b.Id = p.OriginBookingId AND b.TenantId = p.TenantId
    WHERE p.Id = @PurchaseId AND p.TenantId = @TenantId AND b.BranchId <> @BranchId
    UNION ALL
    SELECT 1 FROM BookingPackageReservations r
    JOIN Bookings b ON b.Id = r.BookingId AND b.TenantId = r.TenantId
    WHERE r.CustomerPackageId = @PurchaseId AND r.TenantId = @TenantId
      AND b.BranchId <> @BranchId
)
    THROW 51003, 'Persisted sale or booking evidence contradicts the proposed branch.', 1;

UPDATE CustomerPackages SET BranchId = @BranchId,
    BranchAttributionSource = 'ReviewedDocument'
WHERE Id = @PurchaseId AND TenantId = @TenantId AND BranchId IS NULL;

INSERT INTO AuditLogs (Id, TenantId, UserId, Action, EntityType, EntityId, EntityName,
                       DetailsJson, CreatedAt)
SELECT CONVERT(nvarchar(36), NEWID()), @TenantId, @ActorUserId,
       'PurchaseBranchAttributed', 'CustomerPackage', @PurchaseId, @PurchaseId,
       (SELECT CAST(NULL AS nvarchar(36)) AS oldValue, @BranchId AS newValue,
               @EvidenceReference AS evidenceReference
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES), SYSUTCDATETIME();
COMMIT TRANSACTION;
