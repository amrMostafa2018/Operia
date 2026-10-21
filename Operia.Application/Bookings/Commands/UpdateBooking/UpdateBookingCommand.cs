using MediatR;
using Operia.Application.Bookings.Commands.CreateBooking;

namespace Operia.Application.Bookings.Commands.UpdateBooking;

/// <summary>Requests the update booking state change.</summary>
public sealed record UpdateBookingCommand(
    string BookingId,
    string Version,
    IReadOnlyList<CreateBookingItemInput> Items,
    string? PaymentMethod = null) : IRequest<UpdateBookingResult>;

/// <summary>Carries update booking result data across the operation boundary.</summary>
public sealed record UpdateBookingResult(string Id, string Status, string Version);
