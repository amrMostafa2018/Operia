using MediatR;

namespace Operia.Application.Bookings.Commands.CreateBooking;

/// <summary>Carries create booking item input data across the operation boundary.</summary>
public sealed record CreateBookingItemInput(string PackageId, string? CustomerPackageId, int Quantity, string? Type = null);

/// <summary>Requests the create booking state change.</summary>
public sealed record CreateBookingCommand(
    string IdempotencyKey,
    string CustomerId,
    string BranchId,
    string EmployeeId,
    DateOnly ScheduledDate,
    int StartMinutes,
    int EndMinutes,
    IReadOnlyList<CreateBookingItemInput> Items,
    string? PaymentMethod = null) : IRequest<CreateBookingResult>;

/// <summary>Carries create booking result data across the operation boundary.</summary>
public sealed record CreateBookingResult(string Id, string BookingNumber, string Status, string Version);
