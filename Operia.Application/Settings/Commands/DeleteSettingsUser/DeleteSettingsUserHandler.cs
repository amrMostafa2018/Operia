using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.Application.Settings.Common;

namespace Operia.Application.Settings.Commands.DeleteSettingsUser;

public sealed class DeleteSettingsUserHandler : IRequestHandler<DeleteSettingsUserCommand>
{
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;

    public DeleteSettingsUserHandler(
        IIdentityService identityService,
        ICurrentUserService currentUserService)
    {
        _identityService = identityService;
        _currentUserService = currentUserService;
    }

    public async Task Handle(DeleteSettingsUserCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = SettingsHandlerHelpers.RequireUser(_currentUserService);
        await _identityService.DeleteTenantUserAsync(currentUserId, request.UserId, cancellationToken);
    }
}
