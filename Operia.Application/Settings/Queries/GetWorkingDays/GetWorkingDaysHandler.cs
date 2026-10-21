using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.Application.Settings.Common;

namespace Operia.Application.Settings.Queries.GetWorkingDays;

public sealed class GetWorkingDaysHandler : IRequestHandler<GetWorkingDaysQuery, WorkingDaysSettingsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public GetWorkingDaysHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<WorkingDaysSettingsDto> Handle(GetWorkingDaysQuery request, CancellationToken cancellationToken)
    {
        var tenantId = SettingsHandlerHelpers.RequireTenant(_currentUserService);
        var settings = await SettingsHandlerHelpers.GetBusinessSettingsAsync(_db, tenantId, cancellationToken);
        var days = SettingsHandlerHelpers.Deserialize(settings.WorkingDaysJson, WorkingDayDto.DefaultWeek);
        return new WorkingDaysSettingsDto(
            days.Count == 0 ? WorkingDayDto.DefaultWeek : days,
            settings.AllowBookingOutsideWorkingHours);
    }
}
