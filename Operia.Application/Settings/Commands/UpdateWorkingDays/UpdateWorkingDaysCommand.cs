using MediatR;

namespace Operia.Application.Settings.Commands.UpdateWorkingDays;

public sealed record UpdateWorkingDaysCommand(WorkingDaysSettingsDto Request) : IRequest<WorkingDaysSettingsDto>;
