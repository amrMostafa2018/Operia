using FluentValidation;

namespace Operia.Application.Settings.Commands.UpdateWorkingDays;

public sealed class UpdateWorkingDaysValidator : AbstractValidator<UpdateWorkingDaysCommand>
{
    public UpdateWorkingDaysValidator()
    {
        RuleFor(x => x.Request.Days).Must(days => days.Count == 7 && days.Select(day => day.Day).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 7)
            .WithMessage("Working hours must contain seven unique days.");
        RuleForEach(x => x.Request.Days).Must(day => !day.Enabled || day.FromTime < day.ToTime)
            .WithMessage("Opening time must be before closing time.");
    }
}
