using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Sales.Queries.QuoteSale;

public sealed class QuoteSaleValidator : AbstractValidator<QuoteSaleQuery>
{
    public QuoteSaleValidator()
    {
        RuleFor(query => query).Must(query =>
                !query.DirectCustomer || string.IsNullOrWhiteSpace(query.CustomerId))
            .WithErrorCode(ApiErrorCodes.Sales.DirectCustomerSelectionInvalid);
        RuleFor(query => query.Items)
            .NotEmpty().WithErrorCode(ApiErrorCodes.Sales.ItemsRequired);
        RuleFor(query => query.Items).Must(items => items is null || items.Count(item =>
                string.IsNullOrWhiteSpace(item.OfferId) &&
                string.IsNullOrWhiteSpace(item.OwnedCustomerPackageId)) <= 1)
            .WithErrorCode(ApiErrorCodes.Sales.UnlistedLimit);
        RuleForEach(query => query.Items).ChildRules(item =>
        {
            item.RuleFor(row => row.Quantity)
                .InclusiveBetween(1, 100).WithErrorCode(ApiErrorCodes.Sales.QuantityInvalid);
            item.RuleFor(row => row.PaymentOption)
                .Must(value => value is "cash" or "installment" or "owned")
                .WithErrorCode(ApiErrorCodes.Sales.PaymentOptionInvalid);
            item.RuleFor(row => row.UnlistedName)
                .NotEmpty().WithErrorCode(ApiErrorCodes.Sales.UnlistedNameInvalid)
                .MaximumLength(200).WithErrorCode(ApiErrorCodes.Sales.UnlistedNameInvalid)
                .When(row => string.IsNullOrWhiteSpace(row.OfferId) && string.IsNullOrWhiteSpace(row.OwnedCustomerPackageId));
            item.RuleFor(row => row.UnlistedPrice)
                .NotNull().WithErrorCode(ApiErrorCodes.Sales.UnlistedPriceInvalid)
                .InclusiveBetween(0m, 999999999.99m).WithErrorCode(ApiErrorCodes.Sales.UnlistedPriceInvalid)
                .When(row => string.IsNullOrWhiteSpace(row.OfferId) && string.IsNullOrWhiteSpace(row.OwnedCustomerPackageId));
            item.RuleFor(row => row.UnlistedDurationMinutes)
                .NotNull().WithErrorCode(ApiErrorCodes.Sales.UnlistedDurationInvalid)
                .InclusiveBetween(1, 1440).WithErrorCode(ApiErrorCodes.Sales.UnlistedDurationInvalid)
                .When(row => string.IsNullOrWhiteSpace(row.OfferId) && string.IsNullOrWhiteSpace(row.OwnedCustomerPackageId));
        });
    }
}
