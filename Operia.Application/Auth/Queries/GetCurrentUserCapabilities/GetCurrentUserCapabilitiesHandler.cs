using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Auth.Queries.GetCurrentUserCapabilities;

public sealed class GetCurrentUserCapabilitiesHandler(
    ICurrentUserService currentUserService,
    IPermissionGrantStore permissionGrantStore,
    IApplicationDbContext dbContext)
    : IRequestHandler<GetCurrentUserCapabilitiesQuery, CurrentUserCapabilitiesDto>
{
    public async Task<CurrentUserCapabilitiesDto> Handle(
        GetCurrentUserCapabilitiesQuery request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId
            ?? throw UnauthorizedException.FromCode(ApiErrorCodes.Auth.AuthenticationRequired, "detail");
        var capabilities = await permissionGrantStore.GetUserCapabilitiesAsync(
            userId,
            cancellationToken)
            ?? throw UnauthorizedException.FromCode(ApiErrorCodes.Auth.AuthenticationRequired, "detail");

        string? currencyCode = null;
        var tenantId = currentUserService.TenantId;
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            currencyCode = await dbContext.Tenants
                .AsNoTracking()
                .Where(tenant => tenant.Id == tenantId)
                .Select(tenant => tenant.CurrencyCode)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new CurrentUserCapabilitiesDto(
            capabilities.Roles,
            capabilities.Permissions,
            currencyCode);
    }
}
