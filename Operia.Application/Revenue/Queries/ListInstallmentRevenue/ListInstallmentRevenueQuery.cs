using MediatR;

namespace Operia.Application.Revenue.Queries.ListInstallmentRevenue;

public sealed record ListInstallmentRevenueQuery(int Page = 1, int PageSize = 20)
    : IRequest<InstallmentRevenuePage>;

public sealed record InstallmentRevenuePage(int Total, IReadOnlyList<InstallmentRevenueRow> Items);

public sealed record InstallmentRevenueRow(
    string CollectionId,
    string PurchaseId,
    string CustomerName,
    string OfferName,
    int InstallmentNumber,
    decimal Amount,
    string PaymentMethod,
    DateTime ConfirmedAtUtc);
