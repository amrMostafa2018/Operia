using Operia.Domain.Entities;

namespace Operia.Application.Bookings.Commands.CreateBooking;

/// <summary>Defines the atomic create boundary for slot conflicts and session reservations.</summary>
public interface IBookingConcurrencyStore
{
    /// <summary>
    /// Performs the final idempotency, slot, hold, and session checks under database locks,
    /// then saves the staged booking, balances, history, and audit record together.
    /// </summary>
    Task<Booking> CommitCreateAsync(
        Booking booking,
        IReadOnlyList<BookingPackageReservation> reservations,
        IReadOnlyList<CustomerPackage> newPurchases,
        DateTime utcNow,
        CancellationToken cancellationToken);
}
