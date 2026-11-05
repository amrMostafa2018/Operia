using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.Application.Revenue;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Dashboard.Queries.GetDashboardRevenue;

public sealed class GetDashboardRevenueHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope,
    IDateTimeProvider clock) : IRequestHandler<GetDashboardRevenueQuery, DashboardRevenueDto>
{
    public async Task<DashboardRevenueDto> Handle(
        GetDashboardRevenueQuery request,
        CancellationToken cancellationToken)
    {
        var ledger = await new RevenueLedger(db, currentUser, branchScope)
            .ScopedAsync(null, cancellationToken);
        var periods = await new RevenuePeriods(db, currentUser, clock)
            .CurrentAsync(cancellationToken);
        var today = await ledger.ForPeriodAsync(periods.TodayFromUtc, periods.TodayToUtc,
            cancellationToken);
        var month = await ledger.ForPeriodAsync(periods.MonthFromUtc, periods.MonthToUtc,
            cancellationToken);
        return new DashboardRevenueDto(today.Net, month.Net);
    }
}
