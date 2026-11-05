using MediatR;

namespace Operia.Application.Revenue.Queries.ListActivityRevenue;

public sealed record ListActivityRevenueQuery(int Page = 1, int PageSize = 20, string? BranchId = null)
    : IRequest<ActivityRevenuePage>;

public sealed record ActivityRevenuePage(
    int Total,
    decimal ConfirmedTotal,
    IReadOnlyList<ActivityRevenueRow> Items);

public sealed record ActivityRevenueRow(
    string CollectionId,
    string? SaleId,
    string? PurchaseId,
    string CustomerName,
    string OfferName,
    int? InstallmentNumber,
    decimal Amount,
    string PaymentMethod,
    DateTime ConfirmedAtUtc,
    string Kind);
