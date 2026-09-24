using MediatR;

namespace Operia.Application.Bookings.Commands.CloseBooking;

/// <summary>Requests closing a booked appointment and finalising per-item usage.</summary>
public sealed record CloseBookingCommand(
    string BookingId,
    string Version,
    IReadOnlyList<CloseBookingItemInput> Items) : IRequest<CloseBookingResult>;

/// <summary>Carries one booking line outcome submitted from the close dialog.</summary>
public sealed record CloseBookingItemInput(
    string BookingItemId,
    string? CustomerPackageId,
    string Status,
    int? PulsesUsed,
    string? Notes);

/// <summary>Carries close booking result data across the operation boundary.</summary>
public sealed record CloseBookingResult(string Id, string Status, string Version);
