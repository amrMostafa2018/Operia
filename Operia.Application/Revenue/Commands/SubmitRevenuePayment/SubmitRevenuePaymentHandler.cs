using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Sales.Commands.CollectSale;
using Operia.Domain.Entities;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;
using FluentValidation.Results;

namespace Operia.Application.Revenue.Commands.SubmitRevenuePayment;

public sealed class SubmitRevenuePaymentHandler(
    IApplicationDbContext db,
    ISaleCollectionStore store,
    ICurrentUserService currentUser,
    IBranchScope branchScope,
    IAuditWriter auditWriter,
    IDateTimeProvider clock)
    : IRequestHandler<SubmitRevenuePaymentCommand, SubmittedRevenuePayment>
{
    public async Task<SubmittedRevenuePayment> Handle(
        SubmitRevenuePaymentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        return await store.ExecuteAsync(tenantId, request.SaleId, async token =>
        {
            var sale = await db.Sales.SingleOrDefaultAsync(x =>
                x.TenantId == tenantId && x.Id == request.SaleId, token)
                ?? throw ApiNotFoundException.FromCode(ApiErrorCodes.Sales.NotFound);
            var allowed = await branchScope.GetAllowedBranchIdsAsync(
                currentUser.UserId ?? string.Empty, token);
            if (!allowed.Contains(sale.BranchId))
            {
                throw ForbiddenException.FromCode(ApiErrorCodes.Sales.BranchUnavailable,
                    "branchId");
            }
            if (request.ProofContent is not null &&
                sale.PaymentMethod is not ("bank_transfer" or "instapay" or "e_wallet"))
            {
                throw new ValidationException([
                    new ValidationFailure("proof", ApiErrorCodes.Revenue.ProofInvalid)
                    { ErrorCode = ApiErrorCodes.Revenue.ProofInvalid }
                ]);
            }
            var prior = await db.SaleCollections.AsNoTracking().SingleOrDefaultAsync(x =>
                x.TenantId == tenantId && x.SaleId == sale.Id &&
                x.IdempotencyKey == request.IdempotencyKey, token);
            if (prior is not null)
            {
                if (prior.ReviewStatus == "UnderReview" &&
                    prior.ProofContentType == request.ProofContentType &&
                    ((prior.ProofContent is null && request.ProofContent is null) ||
                     (prior.ProofContent is not null && request.ProofContent is not null &&
                      prior.ProofContent.AsSpan().SequenceEqual(request.ProofContent))))
                {
                    return new SubmittedRevenuePayment(prior.Id, prior.ReviewStatus);
                }
                throw ConflictException.FromCode(ApiErrorCodes.Sales.CollectionAttemptConflict);
            }
            if (sale.Status != "PendingCollection" || sale.DueNow <= 0m ||
                await db.SaleCollections.AnyAsync(x => x.TenantId == tenantId &&
                    x.SaleId == sale.Id && (x.Confirmed || x.ReviewStatus == "UnderReview"), token))
            {
                throw ConflictException.FromCode(ApiErrorCodes.Revenue.ReviewUnavailable);
            }

            var payment = new SaleCollection
            {
                TenantId = tenantId,
                SaleId = sale.Id,
                IdempotencyKey = request.IdempotencyKey,
                Amount = sale.DueNow,
                PaymentMethod = sale.PaymentMethod,
                Confirmed = false,
                ReviewStatus = "UnderReview",
                AttemptedAtUtc = clock.UtcNow,
                CollectedByUserId = currentUser.UserId ?? string.Empty,
                ProofContent = request.ProofContent,
                ProofContentType = request.ProofContentType
            };
            db.SaleCollections.Add(payment);
            auditWriter.Write(tenantId, AuditActions.RevenuePaymentSubmitted,
                nameof(SaleCollection), payment.Id, payment.Id,
                new { payment.SaleId, payment.Amount, payment.PaymentMethod,
                    NewStatus = payment.ReviewStatus, HasProof = payment.ProofContent is not null });
            await db.SaveChangesAsync(token);
            return new SubmittedRevenuePayment(payment.Id, payment.ReviewStatus);
        }, cancellationToken);
    }
}
