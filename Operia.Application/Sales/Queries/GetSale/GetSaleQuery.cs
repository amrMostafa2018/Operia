using MediatR;

namespace Operia.Application.Sales.Queries.GetSale;

public sealed record GetSaleQuery(string SaleId) : IRequest<SaleStateDto>;

public sealed record SaleStateDto(
    string SaleId,
    string Status,
    string Disposition,
    string BranchId,
    int DurationMinutes,
    decimal GrandTotal,
    decimal DueNow,
    string? CollectionId,
    bool PaymentConfirmed,
    string? BookingId);
