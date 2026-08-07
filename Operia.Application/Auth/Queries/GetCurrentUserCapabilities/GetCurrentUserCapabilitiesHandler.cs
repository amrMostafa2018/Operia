using MediatR;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Errors;

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
            ?? throw UnauthorizedException.FromCode(ApiErrorCodes.Auth.AuthenticationRequired, "detail");
        var capabilities = await permissionGrantStore.GetUserCapabilitiesAsync(
            userId,
            cancellationToken)
            ?? throw UnauthorizedException.FromCode(ApiErrorCodes.Auth.AuthenticationRequired, "detail");

        return new CurrentUserCapabilitiesDto(
            capabilities.Roles,
            capabilities.Permissions);
    }
}
