using System.Text.Json;
using MediatR;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Interfaces;
using Operia.Application.Settings.Common;

namespace Operia.Application.Settings.Commands.UpdateWorkingDays;

public sealed class UpdateWorkingDaysHandler : IRequestHandler<UpdateWorkingDaysCommand, WorkingDaysSettingsDto>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditWriter _auditWriter;

    public UpdateWorkingDaysHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUserService,
        IAuditWriter auditWriter)
    {
        _db = db;
        _currentUserService = currentUserService;
        _auditWriter = auditWriter;
    }

    public async Task<WorkingDaysSettingsDto> Handle(UpdateWorkingDaysCommand request, CancellationToken cancellationToken)
    {
        var tenantId = SettingsHandlerHelpers.RequireTenant(_currentUserService);
        var settings = await SettingsHandlerHelpers.GetBusinessSettingsAsync(_db, tenantId, cancellationToken);
        settings.WorkingDaysJson = JsonSerializer.Serialize(request.Request.Days, JsonOptions);
        settings.AllowBookingOutsideWorkingHours = request.Request.AllowBookingOutsideWorkingHours;
        _auditWriter.Write(
            tenantId,
            AuditActions.WorkingDaysUpdated,
            nameof(Operia.Domain.Entities.BusinessSettings),
            settings.Id,
            "Working days",
            new { request.Request.AllowBookingOutsideWorkingHours });
        await _db.SaveChangesAsync(cancellationToken);
        return request.Request;
    }
}
