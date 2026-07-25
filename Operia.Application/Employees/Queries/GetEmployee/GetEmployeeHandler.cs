using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Employees.Common;
using Operia.Domain.Entities;
using Operia.Domain.Exceptions;

namespace Operia.Application.Employees.Queries.GetEmployee;

public sealed class GetEmployeeHandler : IRequestHandler<GetEmployeeQuery, EmployeeDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;

    public GetEmployeeHandler(
        IApplicationDbContext db,
        IIdentityService identityService,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _identityService = identityService;
        _currentUserService = currentUserService;
    }

    public async Task<EmployeeDto> Handle(GetEmployeeQuery request, CancellationToken cancellationToken)
    {
        var tenantId = EmployeeHandlerHelpers.RequireTenant(_currentUserService);
        var employee = await _db.Employees.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.Id);

        var roles = await _identityService.GetRolesAsync([employee.IdentityUserId], cancellationToken);
        var list = await EmployeeHandlerHelpers.MapManyAsync(_db, _identityService, [employee], roles, cancellationToken);
        return list.Single();
    }
}
