using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Customers.Queries.FindCustomerPurchases;

public sealed class FindCustomerPurchasesValidator : AbstractValidator<FindCustomerPurchasesQuery>
{
    public FindCustomerPurchasesValidator()
    {
        RuleFor(x => x.Mobile)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode(ApiErrorCodes.Auth.PhoneRequired)
            .Matches(@"^\+?[\d\s()-]{8,20}$")
            .WithErrorCode(ApiErrorCodes.Auth.PhoneInvalid)
            .Must(mobile => string.Concat(mobile.Where(char.IsDigit)).Length is >= 8 and <= 15)
            .WithErrorCode(ApiErrorCodes.Auth.PhoneInvalid);
    }
}
