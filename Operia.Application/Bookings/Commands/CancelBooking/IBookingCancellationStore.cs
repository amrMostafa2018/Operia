namespace Operia.Application.Bookings.Commands.CancelBooking;

/// <summary>Defines the locked transaction boundary for cancelling a booking and releasing balances.</summary>
public interface IBookingCancellationStore
{
    /// <summary>
    /// Locks the booking slot and affected purchases, then runs the caller's cancellation
    /// and commits its staged state, history, and audit changes as one transaction.
    /// </summary>
    Task<CancelBookingResult> ExecuteAsync(
        string tenantId,
        string employeeId,
        DateOnly scheduledDate,
        IReadOnlyList<string> customerPackageIds,
        Func<CancellationToken, Task<CancelBookingResult>> cancel,
        CancellationToken cancellationToken);
}
