using MediatR;

namespace Operia.Application.Sales.Commands.BookSale;

public sealed record BookSaleCommand(
    string SaleId,
    string EmployeeId,
    DateOnly ScheduledDate,
    int StartMinutes,
    int EndMinutes) : IRequest<BookSaleResult>;

public sealed record BookSaleResult(
    string SaleId,
    string? BookingId,
    string Status,
    string? RecoveryCode);
