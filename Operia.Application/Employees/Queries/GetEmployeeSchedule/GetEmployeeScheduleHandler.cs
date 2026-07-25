using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Interfaces;
using Operia.Application.Employees.Common;

namespace Operia.Application.Employees.Queries.GetEmployeeSchedule;

public sealed class GetEmployeeScheduleHandler : IRequestHandler<GetEmployeeScheduleQuery, EmployeeScheduleDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public GetEmployeeScheduleHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<EmployeeScheduleDto> Handle(GetEmployeeScheduleQuery request, CancellationToken cancellationToken)
    {
        var tenantId = EmployeeHandlerHelpers.RequireTenant(_currentUserService);
        await EmployeeScheduleHelpers.EnsureEmployeeAsync(_db, tenantId, request.EmployeeId, cancellationToken);
        var days = await _db.EmployeeWorkingDays.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.EmployeeId == request.EmployeeId)
            .ToListAsync(cancellationToken);

        return new EmployeeScheduleDto(EmployeeScheduleHelpers.ToWeek(days));
    }
}
