using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages;
using Operia.Domain.Enums;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Customers.Queries.FindCustomerPurchases;

public sealed class FindCustomerPurchasesHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IDateTimeProvider clock) : IRequestHandler<FindCustomerPurchasesQuery, CustomerPurchasesDto?>
{
    public async Task<CustomerPurchasesDto?> Handle(
        FindCustomerPurchasesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw UnauthorizedException.FromCode(ApiErrorCodes.Access.TenantContextRequired, "detail");
        }

        var mobile = string.Concat(request.Mobile.Where(char.IsDigit));
        var customer = await db.Customers.AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDirect &&
                x.NormalizedMobileNumber == mobile)
            .Select(x => new { x.Id, x.FullName, x.MobileNumber, x.CreatedAt, x.IsActive })
            .SingleOrDefaultAsync(cancellationToken);

        if (customer is null)
        {
            return null;
        }

        var purchases = await db.CustomerPackages.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CustomerId == customer.Id)
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.CreatedAt,
                x.ExpiresOn,
                x.IsActive,
                x.Total,
                x.Used,
                x.PurchaseSnapshotJson,
                CurrentOfferName = x.Package!.Name,
                CurrentOfferType = x.Package.OfferType
            })
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(clock.UtcNow);
        var items = purchases.Select(x =>
        {
            var snapshot = PackagePurchaseSnapshot.FromJson(x.PurchaseSnapshotJson);
            var type = snapshot?.OfferType ?? x.CurrentOfferType;
            var status = !x.IsActive ? "cancelled"
                : x.ExpiresOn is not null && x.ExpiresOn < today ? "expired"
                : x.Total > 0 && x.Used >= x.Total ? "completed"
                : "active";

            return new CustomerPurchaseDto(
                x.Id,
                snapshot?.OfferName ?? x.CurrentOfferName,
                type == OfferType.Package ? "package" : "session",
                x.CreatedAt,
                x.ExpiresOn,
                status);
        }).ToArray();

        return new CustomerPurchasesDto(
            customer.Id,
            customer.FullName,
            customer.MobileNumber,
            customer.CreatedAt,
            customer.IsActive ? "active" : "inactive",
            items.Length,
            items);
    }
}
