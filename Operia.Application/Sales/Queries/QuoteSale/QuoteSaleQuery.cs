using MediatR;

namespace Operia.Application.Sales.Queries.QuoteSale;

public sealed record QuoteSaleQuery(
    string? CustomerId,
    string? BranchId,
    IReadOnlyList<QuoteSaleItem> Items,
    bool DirectCustomer = false) : IRequest<SaleQuoteDto>;

public sealed record QuoteSaleItem(
    string? OfferId,
    string? OwnedCustomerPackageId,
    string? UnlistedName,
    decimal? UnlistedPrice,
    int? UnlistedDurationMinutes,
    string PaymentOption,
    int Quantity = 1);

public sealed record SaleQuoteLineDto(
    string? OfferId,
    string? OwnedCustomerPackageId,
    string Name,
    string OfferType,
    int DurationMinutes,
    int? SessionCount,
    int Quantity,
    string PaymentOption,
    decimal ListPrice,
    decimal DiscountAmount,
    decimal FinalAmount,
    decimal DueNow,
    Operia.Application.Packages.PackagePurchaseSnapshot? PurchaseTerms);

public sealed record SaleQuoteDto(
    IReadOnlyList<SaleQuoteLineDto> Items,
    decimal Subtotal,
    decimal Discount,
    decimal GrandTotal,
    decimal DueNow);
