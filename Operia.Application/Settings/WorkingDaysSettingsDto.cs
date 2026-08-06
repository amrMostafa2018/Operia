namespace Operia.Application.Settings;

public sealed record WorkingDayDto(string Day, bool Enabled, TimeOnly FromTime, TimeOnly ToTime)
{
    public static IReadOnlyList<WorkingDayDto> DefaultWeek =>
    [
        new("fri", false, new(9, 0), new(21, 0)),
        new("sat", true, new(9, 0), new(21, 0)),
        new("sun", true, new(9, 0), new(21, 0)),
        new("mon", true, new(9, 0), new(21, 0)),
        new("tue", true, new(9, 0), new(21, 0)),
        new("wed", true, new(9, 0), new(21, 0)),
        new("thu", true, new(9, 0), new(21, 0))
    ];
}

public sealed record WorkingDaysSettingsDto(
    IReadOnlyList<WorkingDayDto> Days,
    bool AllowBookingOutsideWorkingHours);
