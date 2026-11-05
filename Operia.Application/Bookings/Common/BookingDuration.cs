using Operia.Application.Bookings.Commands.CreateBooking;
using Operia.Domain.Entities;
using Operia.Domain.Enums;

namespace Operia.Application.Bookings.Common;

/// <summary>Computes the sequential time required by selected booking items.</summary>
public static class BookingDuration
{
    public static int RequiredMinutes(
        IReadOnlyList<CreateBookingItemInput> items,
        IReadOnlyDictionary<string, Package> catalog)
    {
        long total = 0;
        foreach (var item in items)
        {
            var offer = catalog[item.PackageId];
            // Extra package units are purchases, not additional sessions in this appointment.
            var sessions = offer.OfferType == OfferType.Package ? 1 : item.Quantity;
            total += (long)offer.SessionDurationMinutes * sessions;
            if (total > 1440)
            {
                return 1441;
            }
        }

        return (int)total;
    }
}
