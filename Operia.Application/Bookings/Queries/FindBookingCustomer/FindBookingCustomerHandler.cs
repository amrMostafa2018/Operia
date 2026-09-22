using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Bookings.Common;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Enums;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Bookings.Queries.FindBookingCustomer;

/// <summary>Executes the find booking customer operation within the current tenant.</summary>
public sealed class FindBookingCustomerHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IDateTimeProvider clock) : IRequestHandler<FindBookingCustomerQuery, BookingCustomerDto?>
{
    /// <summary>Finds an active customer and eligible owned balances by normalized mobile.</summary>
    public async Task<BookingCustomerDto?> Handle(FindBookingCustomerQuery request, CancellationToken cancellationToken)
    {
        var tenantId = BookingAccess.RequireTenant(currentUser);
        var normalizedMobile = NormalizeMobile(request.Mobile);
        var today = DateOnly.FromDateTime(clock.UtcNow);

        return await db.Customers
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId &&
                        x.IsActive &&
                        x.NormalizedMobileNumber == normalizedMobile)
            .Select(x => new BookingCustomerDto(
                x.Id,
                x.FullName,
                x.MobileNumber,
                x.Packages
                    .Where(owned => owned.IsActive &&
                                    (owned.ExpiresOn == null || owned.ExpiresOn >= today) &&
                                    owned.Package != null &&
                                    owned.Package.Status == PackageStatus.Active &&
                                    owned.Total > owned.Used + (owned.ReservedSessions ?? 0))
                    .OrderBy(owned => owned.ExpiresOn)
                    .Select(owned => new BookingCustomerPackageDto(
                        owned.Id,
                        owned.PackageId,
                        owned.Package!.Name,
                        owned.Package.SessionDurationMinutes,
                        owned.Total,
                        owned.Used,
                        owned.ReservedSessions ?? 0,
                        owned.Total - owned.Used - (owned.ReservedSessions ?? 0),
                        owned.ExpiresOn,
                        owned.Package.OfferType == OfferType.Package ? "package" : "session"))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>Removes non-digit characters before matching a registered customer's mobile.</summary>
    internal static string NormalizeMobile(string mobile) => string.Concat(mobile.Where(char.IsDigit));
}
