using FluentValidation;
using Operia.Application.Employees.Commands.UpdateEmployeeSchedule;

namespace Operia.Application.Employees.Commands.CreateEmployee;

public sealed class CreateEmployeeValidator : AbstractValidator<CreateEmployeeCommand>
{
    private static readonly string[] WeekDays = ["fri", "sat", "sun", "mon", "tue", "wed", "thu"];

    public CreateEmployeeValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.MobileNumber).NotEmpty().MaximumLength(30);
        RuleFor(x => x.UserName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.TemporaryPassword).NotEmpty().MinimumLength(8);
        RuleFor(x => x.BranchIds).NotEmpty();
        RuleFor(x => x.Role).Must(x => new[] { "SuperAdmin", "Admin", "Reception", "Staff" }.Contains(x));

        When(x => x.Schedule is not null, () =>
        {
            RuleFor(x => x.Schedule)
                .NotEmpty()
                .WithMessage("At least one branch schedule is required.");
            RuleForEach(x => x.Schedule!).ChildRules(branch =>
            {
                branch.RuleFor(x => x.BranchId).NotEmpty();
                branch.RuleFor(x => x.Days)
                    .Must(HaveExactlyOneOfEachWeekDay)
                    .WithMessage("Schedule must contain each business weekday exactly once.");
                branch.RuleForEach(x => x.Days)
                    .Must(HaveValidTimes)
                    .WithMessage("Working days require a start time before the end time; days off must not include times.");
            });
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
