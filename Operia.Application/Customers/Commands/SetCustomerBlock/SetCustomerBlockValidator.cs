using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Customers.Commands.SetCustomerBlock;

public sealed class SetCustomerBlockValidator : AbstractValidator<SetCustomerBlockCommand>
{
    public SetCustomerBlockValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty()
            .WithErrorCode(ApiErrorCodes.Customers.CustomerNotFound)
            .MaximumLength(36)
            .WithErrorCode(ApiErrorCodes.Customers.CustomerNotFound);
        RuleFor(x => x.Reason).MaximumLength(500)
            .WithErrorCode(ApiErrorCodes.Customers.BlockReasonInvalid);
        RuleFor(x => x.Version).NotEmpty()
            .WithErrorCode(ApiErrorCodes.Customers.CustomerChanged);
    }
}
