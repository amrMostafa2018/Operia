using MediatR;

namespace Operia.Application.Bookings.Queries.FindBookingCustomer;

/// <summary>Requests find booking customer data.</summary>
public sealed record FindBookingCustomerQuery(string Mobile, string? BookingId = null) : IRequest<BookingCustomerDto?>;

/// <summary>Carries booking customer package dto data across the operation boundary.</summary>
public sealed record BookingCustomerPackageDto(
    string CustomerPackageId,
    string PackageId,
    string PackageName,
    int SessionDurationMinutes,
    int TotalSessions,
    int UsedSessions,
    int ReservedSessions,
    int AvailableSessions,
    DateOnly? ExpiresOn,
    string OfferType,
    int? SessionCount,
    int? PulseCount);

/// <summary>Carries booking customer dto data across the operation boundary.</summary>
public sealed record BookingCustomerDto(
    string Id,
    string FullName,
    string MobileNumber,
    IReadOnlyList<BookingCustomerPackageDto> Packages);
