using MediatR;

namespace Operia.Application.Branches.Queries.GetBookingBranches;

/// <summary>Requests booking-enabled branches visible to the current user.</summary>
public sealed record GetBookingBranchesQuery : IRequest<IReadOnlyList<BookingBranchDto>>;

/// <summary>Carries the minimal branch identity and name needed by booking screens.</summary>
public sealed record BookingBranchDto(string Id, string Name);
