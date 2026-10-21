using MediatR;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Interfaces;
using Operia.Application.Settings.Common;

namespace Operia.Application.Settings.Commands.ChangePassword;

public sealed class ChangePasswordHandler : IRequestHandler<ChangePasswordCommand>
{
    private readonly IOtpService _otpService;
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _auditWriter;

    public ChangePasswordHandler(
        IOtpService otpService,
        IIdentityService identityService,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IAuditWriter auditWriter)
    {
        _otpService = otpService;
        _identityService = identityService;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _auditWriter = auditWriter;
    }

    public async Task Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var userId = SettingsHandlerHelpers.RequireUser(_currentUserService);
        var tenantId = SettingsHandlerHelpers.RequireTenant(_currentUserService);
        await _otpService.VerifyOtpAsync(userId, request.Request.OtpCode, cancellationToken);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _identityService.ChangePasswordAsync(userId, request.Request.CurrentPassword, request.Request.NewPassword, cancellationToken);
            _auditWriter.Write(tenantId, AuditActions.PasswordChanged, "ApplicationUser", userId, userId);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
