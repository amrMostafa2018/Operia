using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Revenue.Queries.GetActivityRevenueSummary;

public sealed class GetActivityRevenueSummaryHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope,
    IDateTimeProvider clock)
    : IRequestHandler<GetActivityRevenueSummaryQuery, ActivityRevenueSummaryDto>
{
    public async Task<ActivityRevenueSummaryDto> Handle(
        GetActivityRevenueSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var ledger = await new RevenueLedger(db, currentUser, branchScope)
            .ScopedAsync(request.BranchId, cancellationToken);
        var periods = await new RevenuePeriods(db, currentUser, clock)
            .CurrentAsync(cancellationToken);
        var today = await ledger.ForPeriodAsync(periods.TodayFromUtc, periods.TodayToUtc,
            cancellationToken);
        var month = await ledger.ForPeriodAsync(periods.MonthFromUtc, periods.MonthToUtc,
            cancellationToken);
        return new ActivityRevenueSummaryDto(
            new RevenueCardDto(today.Count, today.Gross, today.Refunds, today.Net),
            new RevenueCardDto(month.Count, month.Gross, month.Refunds, month.Net));
    }
}
