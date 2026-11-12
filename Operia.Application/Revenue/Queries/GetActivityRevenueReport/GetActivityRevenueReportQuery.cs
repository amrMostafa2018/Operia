using MediatR;

namespace Operia.Application.Revenue.Queries.GetActivityRevenueReport;

public sealed record GetActivityRevenueReportQuery(
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    string? BranchId = null,
    string? ServiceId = null,
    int Page = 1,
    int PageSize = 10,
    bool AllRows = false,
    string? Search = null,
    string? Status = null,
    string? Mobile = null,
    string? CustomerName = null,
    string? EmployeeId = null) : IRequest<ActivityRevenueReport>;

public sealed record ActivityRevenueReport(
    DateOnly FromDate,
    DateOnly ToDate,
    string TimezoneId,
    int Total,
    int Page,
    int PageSize,
    RevenueReportTotals Totals,
    RevenueReportCard Today,
    RevenueReportCard Month,
    IReadOnlyList<RevenueTransactionRow> Items);

public sealed record RevenueReportTotals(decimal GrossCollections, decimal Refunds, decimal NetCollected);

public sealed record RevenueReportCard(
    int TransactionCount,
    decimal GrossCollections,
    decimal? CountChangePercent = null,
    decimal? AmountChangePercent = null);

public sealed record RevenueTransactionRow(
    string TransactionId,
    string Kind,
    DateTime TransactionAtUtc,
    string CustomerName,
    string Mobile,
    string Service,
    string Branch,
    decimal Amount,
    string PaymentMethod,
    string Status,
    string? SaleId,
    string? PurchaseId,
    string? PaymentId,
    string? RefundId,
    bool HasProof,
    string? CollectedByUserId = null);
