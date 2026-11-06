using MediatR;

namespace Operia.Application.Revenue.Commands.ReviewRevenuePayment;

public sealed record ReviewRevenuePaymentCommand(string PaymentId, bool Confirm)
    : IRequest<ReviewedRevenuePayment>;

public sealed record ReviewedRevenuePayment(string PaymentId, string Status);
