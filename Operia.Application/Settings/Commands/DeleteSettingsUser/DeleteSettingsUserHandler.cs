using MediatR;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Interfaces;
using Operia.Application.Settings.Common;

namespace Operia.Application.Settings.Commands.DeleteSettingsUser;

public sealed class DeleteSettingsUserHandler : IRequestHandler<DeleteSettingsUserCommand>
{
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _auditWriter;

    public DeleteSettingsUserHandler(
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

    public async Task Handle(DeleteSettingsUserCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = SettingsHandlerHelpers.RequireUser(_currentUserService);
        var tenantId = SettingsHandlerHelpers.RequireTenant(_currentUserService);
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _identityService.DeleteTenantUserAsync(currentUserId, request.UserId, cancellationToken);
            _auditWriter.Write(tenantId, AuditActions.TenantUserDeleted, "ApplicationUser", request.UserId, request.UserId);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
