using MediatR;

namespace Operia.Application.Bookings.Queries.ListBookings;

/// <summary>Requests list bookings data.</summary>
public sealed record ListBookingsQuery(
    DateOnly FromDate,
    DateOnly ToDate,
    int PageNumber,
    int PageSize,
    string? Search,
    string? CustomerMobile,
    string? CustomerName,
    string? BranchId,
    string? EmployeeId,
    string? Status) : IRequest<BookingListResult>;

/// <summary>Carries booking list item dto data across the operation boundary.</summary>
public sealed record BookingListItemDto(
    string Id,
    string BookingNumber,
    string CustomerName,
    string CustomerMobile,
    string Service,
    string ServiceType,
    string BranchId,
    string BranchName,
    string EmployeeId,
    string EmployeeName,
    DateOnly ScheduledDate,
    int StartMinutes,
    int EndMinutes,
    string Status,
    string Version,
    string? PaymentMethod,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal DiscountAmount);

/// <summary>Carries booking summary dto data across the operation boundary.</summary>
public sealed record BookingSummaryDto(int Total, int Booked, int Completed, int Cancelled);

/// <summary>Carries booking list result data across the operation boundary.</summary>
public sealed record BookingListResult(
    IReadOnlyList<BookingListItemDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages,
    BookingSummaryDto Summary);
