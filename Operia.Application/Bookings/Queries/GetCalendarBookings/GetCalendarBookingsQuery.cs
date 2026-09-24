using MediatR;

namespace Operia.Application.Bookings.Queries.GetCalendarBookings;

/// <summary>Requests get calendar bookings data.</summary>
public sealed record GetCalendarBookingsQuery(
    string BranchId,
    DateOnly FromDate,
    DateOnly ToDate,
    string? EmployeeId) : IRequest<CalendarBookingsResult>;

/// <summary>Carries calendar booking hold dto data across the operation boundary.</summary>
public sealed record CalendarBookingHoldDto(
    string Id,
    string EmployeeId,
    DateOnly ScheduledDate,
    int StartMinutes,
    int EndMinutes,
    DateTime ExpiresAtUtc);

/// <summary>Carries calendar bookings result data across the operation boundary.</summary>
public sealed record CalendarBookingsResult(
    IReadOnlyList<CalendarBookingDto> Bookings,
    IReadOnlyList<CalendarBookingHoldDto> Holds,
    DateTime ServerNowUtc);

/// <summary>Carries calendar booking item dto data across the operation boundary.</summary>
public sealed record CalendarBookingItemDto(
    string Id,
    string Name,
    string Type,
    int Quantity,
    int DurationMinutes,
    decimal UnitPrice,
    string? PackageId,
    string? CustomerPackageId,
    int? PackageRemainingSessions,
    int? PackagePulseCount,
    int? PackageTotal,
    int? PackageUsed,
    bool PackageSessionLinked);

/// <summary>Carries calendar booking dto data across the operation boundary.</summary>
public sealed record CalendarBookingDto(
    string Id,
    string BookingNumber,
    string Status,
    string CustomerId,
    string CustomerName,
    string CustomerMobile,
    string EmployeeId,
    string EmployeeName,
    string BranchId,
    string BranchName,
    DateOnly ScheduledDate,
    int StartMinutes,
    int EndMinutes,
    string Source,
    string? PaymentMethod,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal DiscountAmount,
    DateTime CreatedAt,
    string Version,
    IReadOnlyList<CalendarBookingItemDto> Items);
