using MediatR;

namespace Operia.Application.Customers.Queries.ListCustomerOperations;

public sealed record CustomerOperationsFilter(
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    string? Customer = null,
    string? Mobile = null,
    string? Service = null,
    string? ServiceType = null,
    string? Status = null,
    string? PaymentType = null);

public sealed record ListCustomerOperationsQuery(
    CustomerOperationsFilter Filter,
    int Page = 1,
    int PageSize = 10) : IRequest<CustomerOperationsPageDto>;

public sealed record CustomerOperationDto(
    string Id,
    string SaleId,
    string CustomerId,
    string CustomerName,
    string Mobile,
    string BranchName,
    string Service,
    string ServiceType,
    DateTime OperationDate,
    string PaymentType,
    decimal? Total,
    decimal? Collected,
    decimal? Remaining,
    int? UnitsTotal,
    int? UnitsRemaining,
    string CustomerRestriction,
    string PurchaseStatus,
    string PaymentStatus,
    string InstallmentDueStatus,
    decimal Refunded = 0m,
    decimal? NetCollected = null,
    string RefundStatus = "none");

public sealed record CustomerOperationsPageDto(
    int TotalCount,
    int Page,
    int PageSize,
    decimal TotalSales,
    decimal TotalCollected,
    decimal TotalRemaining,
    IReadOnlyList<CustomerOperationDto> Rows,
    decimal TotalRefunded = 0m,
    decimal TotalNetCollected = 0m);
