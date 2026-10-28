using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Revenue.Queries.GetActivityRevenueSummary;

public sealed class GetActivityRevenueSummaryValidator : AbstractValidator<GetActivityRevenueSummaryQuery>
{
    public GetActivityRevenueSummaryValidator()
    {
        RuleFor(x => x).Must(x =>
            x.TodayFromUtc < x.TodayToUtc && x.MonthFromUtc < x.MonthToUtc &&
            x.TodayToUtc - x.TodayFromUtc <= TimeSpan.FromDays(2) &&
            x.MonthToUtc - x.MonthFromUtc <= TimeSpan.FromDays(32))
            .WithErrorCode(ApiErrorCodes.Dashboard.RevenueRangeInvalid);
    }
}
