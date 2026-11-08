using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Bookings.Common;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Entities;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Sales.Commands.CollectSale;

public sealed class CollectSaleHandler(
    IApplicationDbContext db,
    ISaleCollectionStore store,
    ICurrentUserService currentUser,
    IBranchScope branchScope,
    IAuditWriter auditWriter,
    IDateTimeProvider clock) : IRequestHandler<CollectSaleCommand, CollectSaleResult>
{
    public async Task<CollectSaleResult> Handle(
        CollectSaleCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);

        return await store.ExecuteAsync(tenantId, request.SaleId, async token =>
        {
            var sale = await db.Sales.SingleOrDefaultAsync(x =>
                x.TenantId == tenantId && x.Id == request.SaleId, token)
                ?? throw ApiNotFoundException.FromCode(ApiErrorCodes.Sales.NotFound);
            var allowedBranches = await branchScope.GetAllowedBranchIdsAsync(
                currentUser.UserId ?? string.Empty, token);
            if (!allowedBranches.Contains(sale.BranchId))
            {
                throw ForbiddenException.FromCode(ApiErrorCodes.Sales.BranchUnavailable,
                    "branchId");
            }
            var priorAttempt = await db.SaleCollections.AsNoTracking().SingleOrDefaultAsync(x =>
                x.TenantId == tenantId && x.SaleId == sale.Id &&
                x.IdempotencyKey == request.IdempotencyKey, token);
            if (priorAttempt is not null)
            {
                if (priorAttempt.Confirmed != request.PaymentConfirmed)
                {
                    throw ConflictException.FromCode(ApiErrorCodes.Sales.CollectionAttemptConflict);
                }
                return Result(sale, priorAttempt);
            }

            var confirmed = await db.SaleCollections.AsNoTracking().SingleOrDefaultAsync(x =>
                x.TenantId == tenantId && x.SaleId == sale.Id && x.Confirmed, token);
            if (confirmed is not null)
            {
                return Result(sale, confirmed);
            }
            if (await db.SaleCollections.AsNoTracking().AnyAsync(x =>
                x.TenantId == tenantId && x.SaleId == sale.Id &&
                x.ReviewStatus == "UnderReview", token))
            {
                throw ConflictException.FromCode(ApiErrorCodes.Sales.CollectionUnavailable);
            }
            if (!SaleCollectionRequirement.RequiresCollection(sale))
            {
                throw ConflictException.FromCode(ApiErrorCodes.Sales.CollectionUnavailable);
            }
            if (sale.Status != "PendingCollection" && sale.Status != "ReadyToBook")
            {
                throw ConflictException.FromCode(ApiErrorCodes.Sales.CollectionUnavailable);
            }
            if (sale.DueNow > 0m)
            {
                var methods = await BookingPaymentMethods.GetEnabledAsync(db, tenantId, token);
                if (!methods.Contains(sale.PaymentMethod, StringComparer.Ordinal))
                {
                    throw ConflictException.FromCode(
                        ApiErrorCodes.Sales.PaymentMethodUnavailable, "paymentMethod");
                }
            }

            var attempt = new SaleCollection
            {
                TenantId = tenantId,
                SaleId = sale.Id,
                IdempotencyKey = request.IdempotencyKey,
                Amount = sale.DueNow,
                PaymentMethod = sale.PaymentMethod,
                Confirmed = request.PaymentConfirmed,
                AttemptedAtUtc = clock.UtcNow,
                ConfirmedAtUtc = request.PaymentConfirmed ? clock.UtcNow : null,
                CollectedByUserId = currentUser.UserId ?? string.Empty
            };
            db.SaleCollections.Add(attempt);

            var oldStatus = sale.Status;
            if (request.PaymentConfirmed)
            {
                await SaleCollectionActivation.ActivateAsync(
                    db, sale, tenantId, clock, token);
            }

            auditWriter.Write(tenantId,
                request.PaymentConfirmed ? AuditActions.SalePaymentConfirmed
                    : AuditActions.SaleCollectionAttempted,
                nameof(Sale), sale.Id, sale.Id,
                new
                {
                    attempt.Id,
                    attempt.Amount,
                    attempt.PaymentMethod,
                    attempt.Confirmed,
                    OldStatus = oldStatus,
                    NewStatus = sale.Status
                });
            await db.SaveChangesAsync(token);
            return Result(sale, attempt);
        }, cancellationToken);
    }

    private static CollectSaleResult Result(Sale sale, SaleCollection attempt) =>
        new(sale.Id, attempt.Id, attempt.Confirmed,
            attempt.Confirmed ? attempt.Amount : 0m, sale.Status);
}
