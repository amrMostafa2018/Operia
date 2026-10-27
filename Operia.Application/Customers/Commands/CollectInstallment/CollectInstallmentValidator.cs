using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Customers.Commands.CollectInstallment;

public sealed class CollectInstallmentValidator : AbstractValidator<CollectInstallmentCommand>
{
    public CollectInstallmentValidator()
    {
        RuleFor(x => x.PurchaseId).NotEmpty()
            .WithErrorCode(ApiErrorCodes.Customers.PurchaseNotFound)
            .MaximumLength(36)
            .WithErrorCode(ApiErrorCodes.Customers.PurchaseNotFound);
        RuleFor(x => x.PaymentMethod).NotEmpty()
            .WithErrorCode(ApiErrorCodes.Customers.InstallmentPaymentMethodUnavailable)
            .MaximumLength(32)
            .WithErrorCode(ApiErrorCodes.Customers.InstallmentPaymentMethodUnavailable);
        RuleFor(x => x.IdempotencyKey).NotEmpty()
            .WithErrorCode(ApiErrorCodes.Customers.InstallmentIdempotencyConflict)
            .MaximumLength(80)
            .WithErrorCode(ApiErrorCodes.Customers.InstallmentIdempotencyConflict);
    }
}
