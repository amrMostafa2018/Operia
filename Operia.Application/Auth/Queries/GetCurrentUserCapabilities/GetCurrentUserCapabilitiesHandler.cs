using MediatR;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Interfaces;

namespace Operia.Application.Auth.Queries.GetCurrentUserCapabilities;

public sealed class GetCurrentUserCapabilitiesHandler(
    ICurrentUserService currentUserService,
    IPermissionGrantStore permissionGrantStore)
    : IRequestHandler<GetCurrentUserCapabilitiesQuery, CurrentUserCapabilitiesDto>
{
    public async Task<CurrentUserCapabilitiesDto> Handle(
        GetCurrentUserCapabilitiesQuery request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId
            ?? throw new UnauthorizedAccessException();
        var capabilities = await permissionGrantStore.GetUserCapabilitiesAsync(
            userId,
            cancellationToken)
            ?? throw new UnauthorizedAccessException();

        return new CurrentUserCapabilitiesDto(
            capabilities.Roles,
            capabilities.Permissions);
    }
}
