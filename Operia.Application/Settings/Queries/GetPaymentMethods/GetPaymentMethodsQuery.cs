using MediatR;

namespace Operia.Application.Settings.Queries.GetPaymentMethods;

public sealed record GetPaymentMethodsQuery : IRequest<PaymentMethodsDto>;
