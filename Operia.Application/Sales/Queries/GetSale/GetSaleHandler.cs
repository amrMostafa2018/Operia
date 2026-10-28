using MediatR;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Errors;
using Operia.Application.Sales.Commands.ConfirmSale;

namespace Operia.Application.Sales.Queries.GetSale;

public sealed class GetSaleHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope) : IRequestHandler<GetSaleQuery, SaleStateDto>
{
    public async Task<SaleStateDto> Handle(GetSaleQuery request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var sale = await db.Sales.AsNoTracking().SingleOrDefaultAsync(x =>
            x.TenantId == tenantId && x.Id == request.SaleId, cancellationToken)
            ?? throw ApiNotFoundException.FromCode(ApiErrorCodes.Sales.NotFound);
        var allowedBranches = await branchScope.GetAllowedBranchIdsAsync(
            currentUser.UserId ?? string.Empty, cancellationToken);
        if (!allowedBranches.Contains(sale.BranchId))
        {
            throw ForbiddenException.FromCode(ApiErrorCodes.Sales.BranchUnavailable,
                "branchId");
        }
        var collection = await db.SaleCollections.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.SaleId == sale.Id)
            .OrderByDescending(x => x.Confirmed).ThenByDescending(x => x.AttemptedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var bookingId = await db.Bookings.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.SaleId == sale.Id)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var lines = JsonSerializer.Deserialize<ConfirmedSaleItemSnapshot[]>(
            sale.ItemsSnapshotJson) ?? [];
        return new SaleStateDto(sale.Id, sale.Status, sale.Disposition,
            sale.BranchId, lines.Sum(x => x.DurationMinutes *
                (x.OfferType == "package" ? 1 : x.Quantity)),
            sale.GrandTotal, sale.DueNow, collection?.Id,
            collection?.Confirmed ?? false, bookingId);
    }
}
