using Operia.Application.Bookings.Commands.CreateBooking;

namespace Operia.Application.Bookings.Commands.UpdateBooking;

/// <summary>Carries update booking request data across the operation boundary.</summary>
public sealed record UpdateBookingRequest(
    string Version,
    IReadOnlyList<CreateBookingItemInput> Items,
    string? PaymentMethod);
