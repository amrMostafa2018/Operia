using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Dashboard.Queries.GetDashboardRevenue;

public sealed class GetDashboardRevenueHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<GetDashboardRevenueQuery, DashboardRevenueDto>
{
    public async Task<DashboardRevenueDto> Handle(
        GetDashboardRevenueQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var sales = db.SaleCollections.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Confirmed);
        var installments = db.InstallmentCollections.AsNoTracking()
            .Where(x => x.TenantId == tenantId);
        var todaySales = await sales.Where(x => x.AttemptedAtUtc >= request.TodayFromUtc &&
            x.AttemptedAtUtc < request.TodayToUtc)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var todayInstallments = await installments.Where(x =>
            x.ConfirmedAtUtc >= request.TodayFromUtc &&
            x.ConfirmedAtUtc < request.TodayToUtc)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var monthSales = await sales.Where(x => x.AttemptedAtUtc >= request.MonthFromUtc &&
            x.AttemptedAtUtc < request.MonthToUtc)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var monthInstallments = await installments.Where(x =>
            x.ConfirmedAtUtc >= request.MonthFromUtc &&
            x.ConfirmedAtUtc < request.MonthToUtc)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        return new DashboardRevenueDto(todaySales + todayInstallments,
            monthSales + monthInstallments);
    }
}
