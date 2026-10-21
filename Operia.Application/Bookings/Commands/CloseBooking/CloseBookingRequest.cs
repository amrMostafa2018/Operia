namespace Operia.Application.Bookings.Commands.CloseBooking;

/// <summary>Carries close booking request data across the operation boundary.</summary>
public sealed record CloseBookingRequest(
    string Version,
    IReadOnlyList<CloseBookingItemInput> Items);
