using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Interfaces;
using Operia.Application.Employees.Common;

namespace Operia.Application.Employees.Queries.GetBookableEmployees;

public sealed class GetBookableEmployeesHandler : IRequestHandler<GetBookableEmployeesQuery, IReadOnlyList<BookableEmployeeDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public GetBookableEmployeesHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<BookableEmployeeDto>> Handle(GetBookableEmployeesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = EmployeeHandlerHelpers.RequireTenant(_currentUserService);
        var query = _db.Employees.AsNoTracking().Where(x => x.TenantId == tenantId && x.IsActive);
        if (!string.IsNullOrWhiteSpace(request.BranchId))
            query = query.Where(x => x.UserBranches.Any(b => b.BranchId == request.BranchId));

        var list = await query.OrderBy(x => x.Code).Select(x => new BookableEmployeeDto(x.Id, x.Code, x.FullName, x.PhotoUrl, x.Specialty, x.JobTitle)).ToListAsync(cancellationToken);
        return list;
    }
}
