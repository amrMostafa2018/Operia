using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Sales.Commands.CollectSale;
using Operia.Domain.Entities;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Revenue.Commands.ReviewRevenuePayment;

public sealed class ReviewRevenuePaymentHandler(
    IApplicationDbContext db,
    ISaleCollectionStore store,
    ICurrentUserService currentUser,
    IBranchScope branchScope,
    IAuditWriter auditWriter,
    IDateTimeProvider clock)
    : IRequestHandler<ReviewRevenuePaymentCommand, ReviewedRevenuePayment>
{
    public async Task<ReviewedRevenuePayment> Handle(
        ReviewRevenuePaymentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var saleId = await db.SaleCollections.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == request.PaymentId)
            .Select(x => x.SaleId).SingleOrDefaultAsync(cancellationToken)
            ?? throw ApiNotFoundException.FromCode(ApiErrorCodes.Revenue.PaymentNotFound);
        return await store.ExecuteAsync(tenantId, saleId, async token =>
        {
            var sale = await db.Sales.SingleOrDefaultAsync(x =>
                x.TenantId == tenantId && x.Id == saleId, token)
                ?? throw ApiNotFoundException.FromCode(ApiErrorCodes.Sales.NotFound);
            var allowed = await branchScope.GetAllowedBranchIdsAsync(
                currentUser.UserId ?? string.Empty, token);
            if (!allowed.Contains(sale.BranchId))
            {
                throw ForbiddenException.FromCode(ApiErrorCodes.Sales.BranchUnavailable,
                    "branchId");
            }
            var payment = await db.SaleCollections.SingleOrDefaultAsync(x =>
                x.TenantId == tenantId && x.Id == request.PaymentId && x.SaleId == sale.Id,
                token) ?? throw ApiNotFoundException.FromCode(ApiErrorCodes.Revenue.PaymentNotFound);
            var target = request.Confirm ? "Paid" : "Cancelled";
            if (payment.ReviewStatus == target)
            {
                return new ReviewedRevenuePayment(payment.Id, target);
            }
            if (payment.ReviewStatus != "UnderReview" ||
                sale.Status != "PendingCollection")
            {
                throw ConflictException.FromCode(ApiErrorCodes.Revenue.ReviewUnavailable);
            }
            if (request.Confirm)
            {
                if (await db.SaleCollections.AnyAsync(x => x.TenantId == tenantId &&
                    x.SaleId == sale.Id && x.Id != payment.Id && x.Confirmed, token))
                {
                    throw ConflictException.FromCode(ApiErrorCodes.Revenue.ReviewUnavailable);
                }
                await SaleCollectionActivation.ActivateAsync(db, sale, tenantId, clock, token);
                payment.Confirmed = true;
                payment.ConfirmedAtUtc = clock.UtcNow;
            }
            payment.ReviewStatus = target;
            payment.ReviewedByUserId = currentUser.UserId;
            payment.ReviewedAtUtc = clock.UtcNow;
            auditWriter.Write(tenantId,
                request.Confirm ? AuditActions.RevenuePaymentConfirmed
                    : AuditActions.RevenuePaymentCancelled,
                nameof(SaleCollection), payment.Id, payment.Id,
                new { payment.SaleId, payment.Amount, OldStatus = "UnderReview",
                    NewStatus = target, payment.ReviewedByUserId, payment.ReviewedAtUtc });
            await db.SaveChangesAsync(token);
            return new ReviewedRevenuePayment(payment.Id, target);
        }, cancellationToken);
    }
}
