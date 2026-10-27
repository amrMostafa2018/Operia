using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Sales.Commands.ConfirmSale;

public sealed class ConfirmSaleValidator : AbstractValidator<ConfirmSaleCommand>
{
    public ConfirmSaleValidator()
    {
        RuleFor(command => command).Must(command =>
                command.DirectCustomer
                    ? string.IsNullOrWhiteSpace(command.CustomerId)
                    : !string.IsNullOrWhiteSpace(command.CustomerId))
            .WithErrorCode(ApiErrorCodes.Sales.DirectCustomerSelectionInvalid);
        RuleFor(command => command.BranchId).NotEmpty()
            .WithErrorCode(ApiErrorCodes.Sales.BranchUnavailable);
        RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(100)
            .WithErrorCode(ApiErrorCodes.Sales.IdempotencyKeyInvalid);
        RuleFor(command => command.Items).NotEmpty()
            .WithErrorCode(ApiErrorCodes.Sales.ItemsRequired);
        RuleFor(command => command.Items).Must(items => items is null || items.Count(item =>
                string.IsNullOrWhiteSpace(item.OfferId) &&
                string.IsNullOrWhiteSpace(item.OwnedCustomerPackageId)) <= 1)
            .WithErrorCode(ApiErrorCodes.Sales.UnlistedLimit);
        RuleForEach(command => command.Items).ChildRules(item =>
        {
            item.RuleFor(row => row.Quantity).InclusiveBetween(1, 100)
                .WithErrorCode(ApiErrorCodes.Sales.QuantityInvalid);
            item.RuleFor(row => row.PaymentOption)
                .Must(value => value is "cash" or "installment" or "owned")
                .WithErrorCode(ApiErrorCodes.Sales.PaymentOptionInvalid);
            item.RuleFor(row => row.UnlistedName).NotEmpty().MaximumLength(200)
                .When(row => string.IsNullOrWhiteSpace(row.OfferId) &&
                    string.IsNullOrWhiteSpace(row.OwnedCustomerPackageId))
                .WithErrorCode(ApiErrorCodes.Sales.UnlistedNameInvalid);
            item.RuleFor(row => row.UnlistedPrice).NotNull().InclusiveBetween(0m, 999999999.99m)
                .When(row => string.IsNullOrWhiteSpace(row.OfferId) &&
                    string.IsNullOrWhiteSpace(row.OwnedCustomerPackageId))
                .WithErrorCode(ApiErrorCodes.Sales.UnlistedPriceInvalid);
            item.RuleFor(row => row.UnlistedDurationMinutes).NotNull().InclusiveBetween(1, 1440)
                .When(row => string.IsNullOrWhiteSpace(row.OfferId) &&
                    string.IsNullOrWhiteSpace(row.OwnedCustomerPackageId))
                .WithErrorCode(ApiErrorCodes.Sales.UnlistedDurationInvalid);
        });
    }
}
