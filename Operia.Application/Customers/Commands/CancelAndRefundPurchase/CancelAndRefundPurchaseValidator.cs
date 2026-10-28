using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Customers.Commands.CancelAndRefundPurchase;

public sealed class CancelAndRefundPurchaseValidator : AbstractValidator<CancelAndRefundPurchaseCommand>
{
    public CancelAndRefundPurchaseValidator()
    {
        RuleFor(x => x.PurchaseId).NotEmpty()
            .WithErrorCode(ApiErrorCodes.Customers.PurchaseNotFound)
            .MaximumLength(36)
            .WithErrorCode(ApiErrorCodes.Customers.PurchaseNotFound);
        RuleFor(x => x.Amount).GreaterThan(0m)
            .WithErrorCode(ApiErrorCodes.Customers.RefundAmountInvalid)
            .PrecisionScale(18, 2, true)
            .WithErrorCode(ApiErrorCodes.Customers.RefundAmountInvalid);
        RuleFor(x => x.IdempotencyKey).NotEmpty()
            .WithErrorCode(ApiErrorCodes.Customers.RefundIdempotencyConflict)
            .MaximumLength(80)
            .WithErrorCode(ApiErrorCodes.Customers.RefundIdempotencyConflict);
        RuleFor(x => x.Note).MaximumLength(1000)
            .WithErrorCode(ApiErrorCodes.Customers.RefundNoteInvalid);
    }
}
