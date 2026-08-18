using FluentValidation;
using Operia.Application.Employees;

namespace Operia.Application.Employees.Commands.UpdateEmployeeSchedule;

public sealed class UpdateEmployeeScheduleValidator : AbstractValidator<UpdateEmployeeScheduleCommand>
{
    private static readonly string[] WeekDays = ["fri", "sat", "sun", "mon", "tue", "wed", "thu"];

    public UpdateEmployeeScheduleValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Branches)
            .NotEmpty()
            .WithMessage("At least one branch schedule is required.");
        RuleForEach(x => x.Branches).ChildRules(branch =>
        {
            branch.RuleFor(x => x.BranchId).NotEmpty();
            branch.RuleFor(x => x.Days)
                .Must(HaveExactlyOneOfEachWeekDay)
                .WithMessage("Schedule must contain each business weekday exactly once.");
            branch.RuleForEach(x => x.Days)
                .Must(HaveValidTimes)
                .WithMessage("Working days require a start time before the end time; days off must not include times.");
        });
    }

    private static bool HaveExactlyOneOfEachWeekDay(IReadOnlyList<EmployeeWorkingDayDto> days)
        => days.Count == WeekDays.Length &&
           days.Select(x => x.Day).Distinct(StringComparer.OrdinalIgnoreCase).Count() == WeekDays.Length &&
           days.All(x => WeekDays.Contains(x.Day, StringComparer.OrdinalIgnoreCase));

    private static bool HaveValidTimes(EmployeeWorkingDayDto day)
        => day.Enabled
            ? day.FromTime is not null && day.ToTime is not null && day.FromTime < day.ToTime
            : day.FromTime is null && day.ToTime is null;
}
