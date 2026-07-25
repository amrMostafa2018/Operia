using MediatR;

namespace Operia.Application.Settings.Commands.UpdatePaymentMethods;

public sealed record UpdatePaymentMethodsCommand(PaymentMethodsDto Request) : IRequest<PaymentMethodsDto>;
