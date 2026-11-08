using MediatR;

namespace Operia.Application.Revenue.Commands.SubmitRevenuePayment;

public sealed record SubmitRevenuePaymentCommand(
    string SaleId, string? IdempotencyKey, byte[]? ProofContent,
    string? ProofContentType) : IRequest<SubmittedRevenuePayment>;

public sealed record SubmittedRevenuePayment(string PaymentId, string Status);
