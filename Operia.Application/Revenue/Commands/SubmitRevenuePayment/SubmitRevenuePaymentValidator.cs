using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Revenue.Commands.SubmitRevenuePayment;

public sealed class SubmitRevenuePaymentValidator : AbstractValidator<SubmitRevenuePaymentCommand>
{
    public SubmitRevenuePaymentValidator()
    {
        RuleFor(x => x.SaleId).NotEmpty()
            .WithErrorCode(ApiErrorCodes.Sales.NotFound);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100)
            .WithErrorCode(ApiErrorCodes.Sales.IdempotencyKeyInvalid);
        RuleFor(x => x).Must(ValidProof)
            .WithErrorCode(ApiErrorCodes.Revenue.ProofInvalid)
            .OverridePropertyName("proof");
    }

    private static bool ValidProof(SubmitRevenuePaymentCommand command)
    {
        var data = command.ProofContent;
        if (data is null) return true;
        if (data.Length is <= 0 or > 5_242_880) return false;
        return command.ProofContentType switch
        {
            "image/jpeg" => data.Length >= 3 && data[0] == 0xff &&
                data[1] == 0xd8 && data[2] == 0xff,
            "image/png" => data.Length >= 8 &&
                data.AsSpan(0, 8).SequenceEqual(
                    new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/webp" => data.Length >= 12 &&
                data.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                data.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
    }
}
