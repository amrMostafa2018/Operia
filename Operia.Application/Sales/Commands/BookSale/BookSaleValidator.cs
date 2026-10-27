using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Sales.Commands.BookSale;

public sealed class BookSaleValidator : AbstractValidator<BookSaleCommand>
{
    public BookSaleValidator()
    {
        RuleFor(x => x.SaleId).NotEmpty().WithErrorCode(ApiErrorCodes.Sales.NotFound);
        RuleFor(x => x.EmployeeId).NotEmpty()
            .WithErrorCode(ApiErrorCodes.Bookings.EmployeeNotFound);
        RuleFor(x => x.ScheduledDate).NotEmpty()
            .WithErrorCode(ApiErrorCodes.Sales.BookingDateInvalid);
        RuleFor(x => x.StartMinutes).InclusiveBetween(0, 1439)
            .WithErrorCode(ApiErrorCodes.Sales.DurationMismatch);
        RuleFor(x => x.EndMinutes).InclusiveBetween(1, 1440)
            .GreaterThan(x => x.StartMinutes)
            .WithErrorCode(ApiErrorCodes.Sales.DurationMismatch);
    }
}
