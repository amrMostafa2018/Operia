using MediatR;

namespace Operia.Application.Bookings.Queries.GetBookingPaymentMethods;

/// <summary>Requests get booking payment methods data.</summary>
public sealed record GetBookingPaymentMethodsQuery : IRequest<IReadOnlyList<string>>;
