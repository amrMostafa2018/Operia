namespace Operia.Application.Bookings.Commands.CancelBooking;

/// <summary>Carries cancel booking request data across the operation boundary.</summary>
public sealed record CancelBookingRequest(string Version);
