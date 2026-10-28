using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.Application.Revenue;

namespace Operia.Application.Dashboard.Queries.GetDashboardRevenue;

public sealed class GetDashboardRevenueHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope) : IRequestHandler<GetDashboardRevenueQuery, DashboardRevenueDto>
{
    public async Task<DashboardRevenueDto> Handle(
        GetDashboardRevenueQuery request,
        CancellationToken cancellationToken)
    {
        var ledger = await new RevenueLedger(db, currentUser, branchScope)
            .ScopedAsync(null, cancellationToken);
        var today = await ledger.ForPeriodAsync(request.TodayFromUtc, request.TodayToUtc,
            cancellationToken);
        var month = await ledger.ForPeriodAsync(request.MonthFromUtc, request.MonthToUtc,
            cancellationToken);
        return new DashboardRevenueDto(today.Net, month.Net);
    }
}
