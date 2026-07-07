using FluentValidation;

namespace Operia.Application.Admin.Commands.ApproveAddBalancePlatform;

public sealed class ApproveAddBalancePlatformCommandValidator
    : AbstractValidator<ApproveAddBalancePlatformCommand>
{
    public ApproveAddBalancePlatformCommandValidator()
    {
        RuleFor(x => x.RevenueId).NotEmpty();
    }
}
