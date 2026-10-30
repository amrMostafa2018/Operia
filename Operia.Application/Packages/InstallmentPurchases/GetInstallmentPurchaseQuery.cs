using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Packages.InstallmentPurchases;

public sealed record GetInstallmentPurchaseQuery(string Id) : IRequest<InstallmentPurchaseDto>;

public sealed class GetInstallmentPurchaseHandler(
    IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetInstallmentPurchaseQuery, InstallmentPurchaseDto>
{
    public async Task<InstallmentPurchaseDto> Handle(
        GetInstallmentPurchaseQuery request, CancellationToken cancellationToken)
    {
        var tenantId = PackageMapper.RequireTenant(currentUser);
        var purchase = await db.CustomerPackages.AsNoTracking()
            .Include(x => x.Installments)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.Id, cancellationToken);
        if (purchase is null || purchase.Installments.Count == 0)
        {
            throw ApiNotFoundException.FromCode(ApiErrorCodes.Packages.PackageNotFound);
        }
        return InstallmentPurchaseMapper.ToDto(purchase);
    }
}
