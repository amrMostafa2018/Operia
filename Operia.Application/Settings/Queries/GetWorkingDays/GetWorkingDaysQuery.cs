using MediatR;

namespace Operia.Application.Settings.Queries.GetWorkingDays;

public sealed record GetWorkingDaysQuery : IRequest<WorkingDaysSettingsDto>;
