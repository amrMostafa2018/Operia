using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Bookings.Common;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages;
using Operia.Domain.Entities;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Customers.Commands.CollectInstallment;

public sealed class CollectInstallmentHandler(
    IApplicationDbContext db,
    IInstallmentCollectionStore store,
    ICurrentUserService currentUser,
    IAuditWriter auditWriter,
    IDateTimeProvider clock) : IRequestHandler<CollectInstallmentCommand, CollectInstallmentResult>
{
    public async Task<CollectInstallmentResult> Handle(
        CollectInstallmentCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw UnauthorizedException.FromCode(ApiErrorCodes.Access.TenantContextRequired, "purchaseId");
        }

        return await store.ExecuteAsync(tenantId, request.PurchaseId, async token =>
        {
            var purchase = await db.CustomerPackages
                .Include(x => x.Installments)
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.PurchaseId, token)
                ?? throw ApiNotFoundException.FromCode(ApiErrorCodes.Customers.PurchaseNotFound);
            var existing = await db.InstallmentCollections.AsNoTracking()
                .SingleOrDefaultAsync(x => x.TenantId == tenantId &&
                                           x.CustomerPackageId == purchase.Id &&
                                           x.IdempotencyKey == request.IdempotencyKey, token);
            if (existing is not null)
            {
                return new CollectInstallmentResult(existing.Id, existing.InstallmentNumber,
                    existing.Amount, purchase.AmountDue ?? 0m,
                    PurchaseSessionEntitlement.Released(purchase), purchase.AmountDue == 0m);
            }

            var snapshot = PackagePurchaseSnapshot.FromJson(purchase.PurchaseSnapshotJson);
            if (snapshot is null || snapshot.PaymentOption != "installment" ||
                snapshot.Installments.Count == 0 || purchase.AmountDue is null ||
                purchase.AmountDue <= 0m)
            {
                throw ConflictException.FromCode(ApiErrorCodes.Customers.InstallmentUnavailable);
            }
            if (!purchase.IsActive ||
                purchase.ExpiresOn is { } expiry && expiry < DateOnly.FromDateTime(clock.UtcNow))
            {
                throw ConflictException.FromCode(ApiErrorCodes.Customers.InstallmentPurchaseInactive);
            }

            var methods = await BookingPaymentMethods.GetEnabledAsync(db, tenantId, token);
            if (!methods.Contains(request.PaymentMethod, StringComparer.Ordinal))
            {
                throw ConflictException.FromCode(
                    ApiErrorCodes.Customers.InstallmentPaymentMethodUnavailable, "paymentMethod");
            }

            var ordered = snapshot.Installments.OrderBy(x => x.Number).ToArray();
            var paid = snapshot.PurchasePrice - purchase.AmountDue.Value;
            var cumulative = snapshot.FirstPayment ?? 0m;
            PurchaseInstallmentSnapshot? next = null;
            foreach (var row in ordered)
            {
                cumulative += row.Amount;
                if (paid < cumulative)
                {
                    next = row;
                    break;
                }
            }
            if (next is null || next.Amount <= 0m || purchase.AmountDue < next.Amount)
            {
                throw ConflictException.FromCode(ApiErrorCodes.Customers.InstallmentUnavailable);
            }
            if (await db.InstallmentCollections.AsNoTracking().AnyAsync(
                    x => x.TenantId == tenantId && x.CustomerPackageId == purchase.Id &&
                         x.InstallmentNumber == next.Number, token))
            {
                throw ConflictException.FromCode(ApiErrorCodes.Customers.InstallmentAlreadyCollected);
            }
            var purchaseRow = purchase.Installments.SingleOrDefault(x => x.Number == next.Number);
            if (purchaseRow is { CollectedAtUtc: not null })
            {
                throw ConflictException.FromCode(ApiErrorCodes.Customers.InstallmentAlreadyCollected);
            }

            var previous = ordered.LastOrDefault(row => row.Number < next.Number);
            if (previous is not null && purchase.Used < previous.CumulativeAvailableSessions)
            {
                throw ConflictException.FromCode(ApiErrorCodes.Customers.InstallmentNotEligible);
            }

            var remaining = purchase.AmountDue.Value - next.Amount;
            var released = InstallmentSessionRelease.AvailableAfterPayment(
                purchase.Total,
                purchase.ReleasedSessions ?? 0,
                next.CumulativeAvailableSessions,
                paymentConfirmed: true,
                fullyPaid: remaining == 0m);
            var collection = new InstallmentCollection
            {
                TenantId = tenantId,
                CustomerPackageId = purchase.Id,
                InstallmentNumber = next.Number,
                Amount = next.Amount,
                PaymentMethod = request.PaymentMethod,
                IdempotencyKey = request.IdempotencyKey,
                CollectedByUserId = currentUser.UserId ?? string.Empty,
                ConfirmedAtUtc = clock.UtcNow
            };
            purchase.AmountDue = remaining;
            purchase.ReleasedSessions = released;
            if (purchaseRow is not null)
            {
                purchaseRow.CollectedAtUtc = collection.ConfirmedAtUtc;
                purchaseRow.PaymentMethod = request.PaymentMethod;
            }
            db.InstallmentCollections.Add(collection);
            auditWriter.Write(tenantId, AuditActions.InstallmentCollected,
                nameof(CustomerPackage), purchase.Id, snapshot.OfferName,
                new
                {
                    collection.Id,
                    collection.InstallmentNumber,
                    collection.Amount,
                    collection.PaymentMethod,
                    Remaining = remaining,
                    ReleasedSessions = released
                });
            await db.SaveChangesAsync(token);
            return new CollectInstallmentResult(collection.Id, next.Number, next.Amount,
                remaining, released, remaining == 0m);
        }, cancellationToken);
    }
}
