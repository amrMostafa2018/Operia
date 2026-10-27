using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Revenue.Queries.ListInstallmentRevenue;

public sealed class ListInstallmentRevenueHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<ListInstallmentRevenueQuery, InstallmentRevenuePage>
{
    public async Task<InstallmentRevenuePage> Handle(
        ListInstallmentRevenueQuery request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw UnauthorizedException.FromCode(ApiErrorCodes.Access.TenantContextRequired, "revenue");
        }

        var query = db.InstallmentCollections.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CustomerPackage != null &&
                        x.CustomerPackage.TenantId == tenantId);
        var total = await query.CountAsync(cancellationToken);
        var page = Math.Clamp(request.Page, 1, 100_000);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var rows = await query.OrderByDescending(x => x.ConfirmedAtUtc).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.CustomerPackageId,
                CustomerName = x.CustomerPackage!.Customer!.FullName,
                OfferName = x.CustomerPackage.Package!.Name,
                x.CustomerPackage.PurchaseSnapshotJson,
                x.InstallmentNumber,
                x.Amount,
                x.PaymentMethod,
                x.ConfirmedAtUtc
            })
            .ToListAsync(cancellationToken);
        return new InstallmentRevenuePage(total, rows.Select(x => new InstallmentRevenueRow(
            x.Id, x.CustomerPackageId, x.CustomerName,
            PackagePurchaseSnapshot.FromJson(x.PurchaseSnapshotJson)?.OfferName ?? x.OfferName,
            x.InstallmentNumber, x.Amount, x.PaymentMethod, x.ConfirmedAtUtc)).ToArray());
    }
}
