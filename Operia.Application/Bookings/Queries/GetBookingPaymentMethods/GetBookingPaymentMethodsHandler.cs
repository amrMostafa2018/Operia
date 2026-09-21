using MediatR;
using Operia.Application.Bookings.Common;
using Operia.Application.Common.Interfaces;

namespace Operia.Application.Bookings.Queries.GetBookingPaymentMethods;

/// <summary>Executes the get booking payment methods operation within the current tenant.</summary>
public sealed class GetBookingPaymentMethodsHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser)
    : IRequestHandler<GetBookingPaymentMethodsQuery, IReadOnlyList<string>>
{
    /// <summary>Returns enabled payment method identifiers for the current tenant.</summary>
    public Task<IReadOnlyList<string>> Handle(
        GetBookingPaymentMethodsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = BookingAccess.RequireTenant(currentUser);
        return BookingPaymentMethods.GetEnabledAsync(db, tenantId, cancellationToken);
    }
}
