using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.Application.Settings.Common;

namespace Operia.Application.Settings.Queries.GetPaymentMethods;

public sealed class GetPaymentMethodsHandler : IRequestHandler<GetPaymentMethodsQuery, PaymentMethodsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public GetPaymentMethodsHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<PaymentMethodsDto> Handle(GetPaymentMethodsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = SettingsHandlerHelpers.RequireTenant(_currentUserService);
        var settings = await SettingsHandlerHelpers.GetBusinessSettingsAsync(_db, tenantId, cancellationToken);
        return SettingsHandlerHelpers.Deserialize(settings.PaymentMethodsJson, PaymentMethodsDto.Default);
    }
}
