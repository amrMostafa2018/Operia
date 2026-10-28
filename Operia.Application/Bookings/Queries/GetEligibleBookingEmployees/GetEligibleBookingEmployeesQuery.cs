using MediatR;
using Operia.Application.Bookings.Commands.CreateBooking;

namespace Operia.Application.Bookings.Queries.GetEligibleBookingEmployees;

/// <summary>Requests staff who can perform every item throughout the proposed interval.</summary>
public sealed record GetEligibleBookingEmployeesQuery(
    string BranchId,
    DateOnly ScheduledDate,
    int StartMinutes,
    int EndMinutes,
    IReadOnlyList<CreateBookingItemInput> Items)
    : IRequest<IReadOnlyList<EligibleBookingEmployee>>;

public sealed record EligibleBookingEmployee(string Id, string FullName);
