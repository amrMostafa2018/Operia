using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Interfaces;
using Operia.Application.Settings.Common;

namespace Operia.Application.Settings.Queries.GetIdentitySettings;

public sealed class GetIdentitySettingsHandler : IRequestHandler<GetIdentitySettingsQuery, IdentitySettingsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public GetIdentitySettingsHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<IdentitySettingsDto> Handle(GetIdentitySettingsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = SettingsHandlerHelpers.RequireTenant(_currentUserService);
        var business = await SettingsHandlerHelpers.GetBusinessAsync(_db, tenantId, cancellationToken);
        var photos = await _db.BusinessGalleries.AsNoTracking()
            .Where(x => x.BusinessId == business.Id)
            .OrderByDescending(x => x.IsMainImage)
            .ThenBy(x => x.UploadedAt)
            .ToListAsync(cancellationToken);

        return SettingsHandlerHelpers.ToIdentity(business, photos);
    }
}
