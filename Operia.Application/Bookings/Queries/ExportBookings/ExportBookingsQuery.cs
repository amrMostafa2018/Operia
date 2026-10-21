using MediatR;

namespace Operia.Application.Bookings.Queries.ExportBookings;

/// <summary>Requests export bookings data.</summary>
public sealed record ExportBookingsQuery(
    DateOnly FromDate,
    DateOnly ToDate,
    string? CustomerMobile,
    string? CustomerName,
    string? EmployeeId,
    string? Status) : IRequest<string>;
