using MediatR;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Interfaces;
using Operia.Application.Settings.Common;

namespace Operia.Application.Settings.Commands.DeactivateAccount;

public sealed class DeactivateAccountHandler : IRequestHandler<DeactivateAccountCommand>
{
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _auditWriter;

    public DeactivateAccountHandler(
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

    public async Task Handle(DeactivateAccountCommand request, CancellationToken cancellationToken)
    {
        var userId = SettingsHandlerHelpers.RequireUser(_currentUserService);
        var tenantId = SettingsHandlerHelpers.RequireTenant(_currentUserService);
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _identityService.DeactivateAccountAsync(userId, cancellationToken);
            _auditWriter.Write(tenantId, AuditActions.AccountDeactivated, "ApplicationUser", userId, userId);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
