using MediatR;

namespace Operia.Application.Revenue.Queries.GetRevenuePaymentProof;

public sealed record GetRevenuePaymentProofQuery(string PaymentId) : IRequest<RevenuePaymentProof>;

public sealed record RevenuePaymentProof(byte[] Content, string ContentType);
