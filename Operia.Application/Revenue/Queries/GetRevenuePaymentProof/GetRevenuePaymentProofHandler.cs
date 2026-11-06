using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Revenue.Queries.GetRevenuePaymentProof;

public sealed class GetRevenuePaymentProofHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IBranchScope branchScope)
    : IRequestHandler<GetRevenuePaymentProofQuery, RevenuePaymentProof>
{
    public async Task<RevenuePaymentProof> Handle(
        GetRevenuePaymentProofQuery request, CancellationToken token)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var allowed = await branchScope.GetAllowedBranchIdsAsync(
            currentUser.UserId ?? string.Empty, token);
        var proof = await (
            from payment in db.SaleCollections.AsNoTracking()
            join sale in db.Sales.AsNoTracking() on payment.SaleId equals sale.Id
            where payment.TenantId == tenantId && sale.TenantId == tenantId &&
                  payment.Id == request.PaymentId
            select new { sale.BranchId, payment.ProofContent, payment.ProofContentType })
            .SingleOrDefaultAsync(token);
        if (proof is null)
        {
            throw ApiNotFoundException.FromCode(ApiErrorCodes.Revenue.PaymentNotFound);
        }
        if (!allowed.Contains(proof.BranchId))
        {
            throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantAccessDenied);
        }
        if (proof.ProofContent is null || proof.ProofContentType is null)
        {
            throw ApiNotFoundException.FromCode(ApiErrorCodes.Revenue.ProofNotFound);
        }
        return new RevenuePaymentProof(proof.ProofContent, proof.ProofContentType);
    }
}
