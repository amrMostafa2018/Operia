using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Sales.Commands.CollectSale;

public sealed class CollectSaleValidator : AbstractValidator<CollectSaleCommand>
{
    public CollectSaleValidator()
    {
        RuleFor(x => x.SaleId).NotEmpty()
            .WithErrorCode(ApiErrorCodes.Sales.NotFound);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100)
            .WithErrorCode(ApiErrorCodes.Sales.IdempotencyKeyInvalid);
    }
}
