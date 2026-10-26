using MediatR;

namespace Operia.Application.Customers.Queries.FindCustomerPurchases;

public sealed record FindCustomerPurchasesQuery(string Mobile) : IRequest<CustomerPurchasesDto?>;

public sealed record CustomerPurchasesDto(
    string CustomerId,
    string FullName,
    string MobileNumber,
    DateTime RegisteredAt,
    string MembershipStatus,
    int OperationCount,
    IReadOnlyList<CustomerPurchaseDto> Purchases);

public sealed record CustomerPurchaseDto(
    string Id,
    string OfferName,
    string OfferType,
    DateTime PurchasedAt,
    DateOnly? ExpiresOn,
    string Status);
