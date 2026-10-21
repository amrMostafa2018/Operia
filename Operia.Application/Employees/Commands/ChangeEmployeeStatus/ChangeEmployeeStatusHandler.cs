using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Employees.Common;
using Operia.Domain.Entities;
using Operia.Domain.Exceptions;

namespace Operia.Application.Employees.Commands.ChangeEmployeeStatus;

public sealed class ChangeEmployeeStatusHandler : IRequestHandler<ChangeEmployeeStatusCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IIdentityService _identityService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditWriter _auditWriter;

    public ChangeEmployeeStatusHandler(
        IApplicationDbContext db,
        IIdentityService identityService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IAuditWriter auditWriter)
    {
        _db = db;
        _identityService = identityService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _auditWriter = auditWriter;
    }

    public async Task Handle(ChangeEmployeeStatusCommand request, CancellationToken cancellationToken)
    {
        var tenantId = EmployeeHandlerHelpers.RequireTenant(_currentUserService);
        var employee = await _db.Employees
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.Id);

        if (employee.IsActive == request.IsActive)
            return;

        if (!request.IsActive && employee.IdentityUserId == _currentUserService.UserId)
            throw new ConflictException("You cannot deactivate your own employee account.");

        var rolesMap = await _identityService.GetRolesAsync([employee.IdentityUserId], cancellationToken);
        var role = rolesMap.GetValueOrDefault(employee.IdentityUserId) ?? string.Empty;

        await EmployeeHandlerHelpers.ProtectLastSuperAdminAsync(_db, _identityService, tenantId, employee, role, !request.IsActive, cancellationToken);
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var oldStatus = employee.IsActive;
            employee.IsActive = request.IsActive;
            await _identityService.SetStatusAsync(employee.IdentityUserId, request.IsActive, cancellationToken);
            EmployeeHandlerHelpers.AddAudit(_auditWriter, tenantId, AuditActions.EmployeeStatusChanged, employee, new { oldStatus, newStatus = request.IsActive });
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
