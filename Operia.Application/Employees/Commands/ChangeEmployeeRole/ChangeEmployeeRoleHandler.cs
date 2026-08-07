using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Employees.Common;
using Operia.Domain.Entities;
using Operia.Domain.Exceptions;

namespace Operia.Application.Employees.Commands.ChangeEmployeeRole;

public sealed class ChangeEmployeeRoleHandler : IRequestHandler<ChangeEmployeeRoleCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IIdentityService _identityService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditWriter _auditWriter;

    public ChangeEmployeeRoleHandler(
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

    public async Task Handle(ChangeEmployeeRoleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = EmployeeHandlerHelpers.RequireTenant(_currentUserService);
        EmployeeHandlerHelpers.ValidateRole(request.Role);

        var employee = await _db.Employees.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.Id);

        var rolesMap = await _identityService.GetRolesAsync([employee.IdentityUserId], cancellationToken);
        var oldRole = rolesMap.GetValueOrDefault(employee.IdentityUserId) ?? string.Empty;

        if (string.Equals(oldRole, request.Role, StringComparison.OrdinalIgnoreCase))
            return;

        await EmployeeHandlerHelpers.ProtectLastSuperAdminAsync(_db, _identityService, tenantId, employee, oldRole, false, cancellationToken);
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _identityService.ChangeRoleAsync(employee.IdentityUserId, request.Role, cancellationToken);
            EmployeeHandlerHelpers.AddAudit(_auditWriter, tenantId, AuditActions.EmployeeRoleChanged, employee, new { oldRole, newRole = request.Role });
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
