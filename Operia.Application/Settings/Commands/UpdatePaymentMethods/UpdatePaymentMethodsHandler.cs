using System.Text.Json;
using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.Application.Settings.Common;

namespace Operia.Application.Settings.Commands.UpdatePaymentMethods;

public sealed class UpdatePaymentMethodsHandler : IRequestHandler<UpdatePaymentMethodsCommand, PaymentMethodsDto>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public UpdatePaymentMethodsHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<PaymentMethodsDto> Handle(UpdatePaymentMethodsCommand request, CancellationToken cancellationToken)
    {
        var tenantId = SettingsHandlerHelpers.RequireTenant(_currentUserService);
        var settings = await SettingsHandlerHelpers.GetBusinessSettingsAsync(_db, tenantId, cancellationToken);
        settings.PaymentMethodsJson = JsonSerializer.Serialize(request.Request, JsonOptions);
        await _db.SaveChangesAsync(cancellationToken);
        return request.Request;
    }
}
