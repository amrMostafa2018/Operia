using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Dashboard.Queries.GetDashboardRevenue;

public sealed class GetDashboardRevenueValidator : AbstractValidator<GetDashboardRevenueQuery>
{
    public GetDashboardRevenueValidator()
    {
        RuleFor(x => x).Must(x =>
            x.TodayFromUtc < x.TodayToUtc && x.MonthFromUtc < x.MonthToUtc &&
            x.TodayToUtc - x.TodayFromUtc <= TimeSpan.FromDays(2) &&
            x.MonthToUtc - x.MonthFromUtc <= TimeSpan.FromDays(32))
            .WithErrorCode(ApiErrorCodes.Dashboard.RevenueRangeInvalid);
    }
}
