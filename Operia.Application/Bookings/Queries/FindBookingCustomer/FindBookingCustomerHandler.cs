using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Bookings.Common;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages;
using Operia.Domain.Enums;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Bookings.Queries.FindBookingCustomer;

/// <summary>Finds customer purchases whose confirmed entitlement can support booking.</summary>
public sealed class FindBookingCustomerHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IDateTimeProvider clock) : IRequestHandler<FindBookingCustomerQuery, BookingCustomerDto?>
{
    public async Task<BookingCustomerDto?> Handle(
        FindBookingCustomerQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = BookingAccess.RequireTenant(currentUser);
        var normalizedMobile = NormalizeMobile(request.Mobile);
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var customer = await db.Customers.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive &&
                        x.NormalizedMobileNumber == normalizedMobile)
            .Select(x => new { x.Id, x.FullName, x.MobileNumber })
            .SingleOrDefaultAsync(cancellationToken);
        if (customer is null)
        {
            return null;
        }

        var purchases = await db.CustomerPackages.AsNoTracking()
            .Include(x => x.Package)
            .Include(x => x.Reservations)
            .Where(x => x.TenantId == tenantId && x.CustomerId == customer.Id &&
                        x.IsActive && (x.ExpiresOn == null || x.ExpiresOn >= today) &&
                        x.Package != null && x.Package.Status == PackageStatus.Active)
            .OrderBy(x => x.ExpiresOn)
            .ToListAsync(cancellationToken);
        var rows = purchases.Select(owned =>
        {
            var offer = owned.Package!;
            var snapshot = PackagePurchaseSnapshot.FromJson(owned.PurchaseSnapshotJson);
            var offerType = snapshot?.OfferType ?? offer.OfferType;
            var pulseCount = snapshot?.PulseCount ?? offer.PulseCount;
            var released = PurchaseSessionEntitlement.Released(owned);
            var available = CustomerPackageBalance.Remaining(owned.Total, owned.Used,
                owned.ReservedSessions, offerType, pulseCount, released);
            return new
            {
                Owned = owned,
                Offer = offer,
                Snapshot = snapshot,
                OfferType = offerType,
                PulseCount = pulseCount,
                Available = available
            };
        })
        .Where(x => x.Available > 0 || request.BookingId is not null &&
            x.Owned.Reservations.Any(r => r.BookingId == request.BookingId))
        .Select(x => new BookingCustomerPackageDto(
            x.Owned.Id,
            x.Owned.PackageId,
            x.Snapshot?.OfferName ?? x.Offer.Name,
            x.Snapshot?.SessionDurationMinutes ?? x.Offer.SessionDurationMinutes,
            x.Owned.Total,
            x.Owned.Used,
            x.Owned.ReservedSessionCount,
            x.Available,
            x.Owned.ExpiresOn,
            x.OfferType == OfferType.Package ? "package" : "session",
            x.Snapshot?.SessionCount ?? x.Offer.SessionCount,
            x.PulseCount))
        .ToArray();
        return new BookingCustomerDto(customer.Id, customer.FullName, customer.MobileNumber, rows);
    }

    internal static string NormalizeMobile(string mobile) => string.Concat(mobile.Where(char.IsDigit));
}
