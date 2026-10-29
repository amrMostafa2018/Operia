using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Revenue.Commands.ReviewRevenuePayment;

public sealed class ReviewRevenuePaymentValidator : AbstractValidator<ReviewRevenuePaymentCommand>
{
    public ReviewRevenuePaymentValidator()
    {
        RuleFor(x => x.PaymentId).NotEmpty()
            .WithErrorCode(ApiErrorCodes.Revenue.PaymentNotFound);
    }
}
