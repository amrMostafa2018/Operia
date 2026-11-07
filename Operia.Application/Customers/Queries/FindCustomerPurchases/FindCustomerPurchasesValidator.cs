using FluentValidation;
using Operia.Application.Common.PhoneNumbers;
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
            .Must(PhoneNumberHelper.IsValidMobile)
            .WithErrorCode(ApiErrorCodes.Auth.PhoneInvalid);
    }
}
