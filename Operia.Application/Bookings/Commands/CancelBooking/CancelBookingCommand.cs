using MediatR;

namespace Operia.Application.Bookings.Commands.CancelBooking;

/// <summary>Requests the cancel booking state change.</summary>
public sealed record CancelBookingCommand(string BookingId, string Version) : IRequest<CancelBookingResult>;

/// <summary>Carries cancel booking result data across the operation boundary.</summary>
public sealed record CancelBookingResult(string Id, string Status, string Version);
