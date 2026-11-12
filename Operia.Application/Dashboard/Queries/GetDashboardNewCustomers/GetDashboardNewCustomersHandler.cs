using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Interfaces;
using Operia.Application.Revenue;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Dashboard.Queries.GetDashboardNewCustomers;

public sealed class GetDashboardNewCustomersHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IDateTimeProvider clock) : IRequestHandler<GetDashboardNewCustomersQuery, DashboardNewCustomersDto>
{
    public async Task<DashboardNewCustomersDto> Handle(
        GetDashboardNewCustomersQuery request,
        CancellationToken cancellationToken)
    {
        var periods = await new RevenuePeriods(db, currentUser, clock)
            .CurrentAsync(cancellationToken);
        var tenantId = currentUser.TenantId!;
        var count = await db.Customers.AsNoTracking()
            .CountAsync(customer =>
                customer.TenantId == tenantId &&
                !customer.IsDirect &&
                customer.CreatedAt >= periods.MonthFromUtc &&
                customer.CreatedAt < periods.MonthToUtc,
                cancellationToken);
        return new DashboardNewCustomersDto(count);
    }
}
