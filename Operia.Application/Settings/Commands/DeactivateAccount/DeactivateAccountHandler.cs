using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.Application.Settings.Common;

namespace Operia.Application.Settings.Commands.DeactivateAccount;

public sealed class DeactivateAccountHandler : IRequestHandler<DeactivateAccountCommand>
{
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;

    public DeactivateAccountHandler(
        IIdentityService identityService,
        ICurrentUserService currentUserService)
    {
        _identityService = identityService;
        _currentUserService = currentUserService;
    }

    public async Task Handle(DeactivateAccountCommand request, CancellationToken cancellationToken)
    {
        var userId = SettingsHandlerHelpers.RequireUser(_currentUserService);
        await _identityService.DeactivateAccountAsync(userId, cancellationToken);
    }
}
