using MediatR;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Interfaces;
using Operia.Application.Settings.Common;

namespace Operia.Application.Settings.Commands.UpdateSecuritySettings;

public sealed class UpdateSecuritySettingsHandler : IRequestHandler<UpdateSecuritySettingsCommand>
{
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _auditWriter;

    public UpdateSecuritySettingsHandler(
        IIdentityService identityService,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IAuditWriter auditWriter)
    {
        _identityService = identityService;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _auditWriter = auditWriter;
    }

    public async Task Handle(UpdateSecuritySettingsCommand request, CancellationToken cancellationToken)
    {
        var userId = SettingsHandlerHelpers.RequireUser(_currentUserService);
        var tenantId = SettingsHandlerHelpers.RequireTenant(_currentUserService);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _identityService.UpdateSecurityUserSettingsAsync(
                userId,
                request.Request.EnableTwoFactorAuthentication,
                request.Request.LoginAlertsEnabled,
                request.Request.LogoutOtherDevices,
                cancellationToken);
            _auditWriter.Write(
                tenantId,
                AuditActions.SecuritySettingsUpdated,
                "ApplicationUser",
                userId,
                userId,
                new
                {
                    request.Request.EnableTwoFactorAuthentication,
                    request.Request.LoginAlertsEnabled,
                    request.Request.LogoutOtherDevices
                });
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
