using MediatR;

namespace Operia.Application.Bookings.Queries.GetBookingHistory;

/// <summary>Requests get booking history data.</summary>
public sealed record GetBookingHistoryQuery(string BookingId) : IRequest<IReadOnlyList<BookingHistoryDto>>;

/// <summary>Carries booking history dto data across the operation boundary.</summary>
public sealed record BookingHistoryDto(
    string Id,
    string Action,
    string? ChangedByUserId,
    string? ChangedByDisplayName,
    DateTime OccurredAt,
    string? ChangesJson);
