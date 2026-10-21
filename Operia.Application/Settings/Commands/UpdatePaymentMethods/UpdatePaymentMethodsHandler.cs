using System.Text.Json;
using MediatR;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Interfaces;
using Operia.Application.Settings.Common;

namespace Operia.Application.Settings.Commands.UpdatePaymentMethods;

public sealed class UpdatePaymentMethodsHandler : IRequestHandler<UpdatePaymentMethodsCommand, PaymentMethodsDto>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditWriter _auditWriter;

    public UpdatePaymentMethodsHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUserService,
        IAuditWriter auditWriter)
    {
        _db = db;
        _currentUserService = currentUserService;
        _auditWriter = auditWriter;
    }

    public async Task<PaymentMethodsDto> Handle(UpdatePaymentMethodsCommand request, CancellationToken cancellationToken)
    {
        var tenantId = SettingsHandlerHelpers.RequireTenant(_currentUserService);
        var settings = await SettingsHandlerHelpers.GetBusinessSettingsAsync(_db, tenantId, cancellationToken);
        settings.PaymentMethodsJson = JsonSerializer.Serialize(request.Request, JsonOptions);
        _auditWriter.Write(
            tenantId,
            AuditActions.PaymentMethodsUpdated,
            nameof(Operia.Domain.Entities.BusinessSettings),
            settings.Id,
            "Payment methods");
        await _db.SaveChangesAsync(cancellationToken);
        return request.Request;
    }
}
