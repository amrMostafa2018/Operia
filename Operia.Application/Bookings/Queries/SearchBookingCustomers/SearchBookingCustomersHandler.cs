using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Bookings.Common;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Bookings.Queries.SearchBookingCustomers;

public sealed class SearchBookingCustomersHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser)
    : IRequestHandler<SearchBookingCustomersQuery, IReadOnlyList<BookingCustomerSearchResult>>
{
    public async Task<IReadOnlyList<BookingCustomerSearchResult>> Handle(
        SearchBookingCustomersQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = BookingAccess.RequireTenant(currentUser);
        var term = request.Term.Trim();
        var digits = string.Concat(term.Where(char.IsDigit));
        var mobileTerm = digits.StartsWith("01", StringComparison.Ordinal)
            ? "20" + digits[1..]
            : digits;
        return await db.Customers.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && !x.IsDirect &&
                (x.FullName.Contains(term) || x.NormalizedMobileNumber.Contains(term) ||
                 digits.Length >= 2 && x.NormalizedMobileNumber.Contains(mobileTerm)))
            .OrderBy(x => x.FullName)
            .ThenBy(x => x.MobileNumber)
            .Take(10)
            .Select(x => new BookingCustomerSearchResult(x.Id, x.FullName, x.MobileNumber))
            .ToListAsync(cancellationToken);
    }
}
