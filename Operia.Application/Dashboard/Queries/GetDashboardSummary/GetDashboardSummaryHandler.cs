using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Dashboard.Queries.GetDashboardSummary;

public sealed class GetDashboardSummaryHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope,
    IDateTimeProvider clock) : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    public async Task<DashboardSummaryDto> Handle(
        GetDashboardSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var metrics = await new DashboardCardMetrics(db, currentUser, branchScope, clock)
            .ComputeAsync(cancellationToken);

        return new DashboardSummaryDto(
            metrics.NewCustomersMonth,
            metrics.TodayBookings,
            metrics.MonthlyBookings,
            metrics.TodayRevenue,
            metrics.MonthlyRevenue);
    }
}
