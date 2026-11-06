using Microsoft.EntityFrameworkCore;
using FluentValidation.Results;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Revenue;

/// <summary>Current calendar periods in the authenticated tenant's saved business timezone.</summary>
internal sealed class RevenuePeriods(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IDateTimeProvider clock)
{
    public async Task<RevenuePeriodBounds> CurrentAsync(CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var timezoneId = await db.Tenants.AsNoTracking()
            .Where(x => x.Id == tenantId)
            .Select(x => x.Timezone)
            .SingleOrDefaultAsync(cancellationToken);
        if (timezoneId is null)
        {
            throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        }

        var timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        var utcNow = DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, timezone);
        var today = localNow.Date;
        var month = new DateTime(today.Year, today.Month, 1);
        return new RevenuePeriodBounds(
            ToUtc(today, timezone), ToUtc(today.AddDays(1), timezone),
            ToUtc(month, timezone), ToUtc(month.AddMonths(1), timezone));
    }

    public async Task<RevenueReportBounds> ReportAsync(
        DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var timezoneId = await db.Tenants.AsNoTracking()
            .Where(x => x.Id == tenantId)
            .Select(x => x.Timezone)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc), timezone));
        var from = fromDate ?? new DateOnly(today.Year, today.Month, 1);
        var to = toDate ?? today;
        if (from > to || to == DateOnly.MaxValue)
        {
            throw new ValidationException([
                new ValidationFailure("fromDate", ApiErrorCodes.Revenue.DateRangeInvalid)
                { ErrorCode = ApiErrorCodes.Revenue.DateRangeInvalid }
            ]);
        }

        return new RevenueReportBounds(from, to, timezone.Id,
            ToUtc(from.ToDateTime(TimeOnly.MinValue), timezone),
            ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), timezone));
    }

    private static DateTime ToUtc(DateTime local, TimeZoneInfo timezone)
    {
        while (timezone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        if (timezone.IsAmbiguousTime(local))
        {
            return new DateTimeOffset(local, timezone.GetAmbiguousTimeOffsets(local).Max())
                .UtcDateTime;
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, timezone);
    }
}

internal sealed record RevenuePeriodBounds(
    DateTime TodayFromUtc,
    DateTime TodayToUtc,
    DateTime MonthFromUtc,
    DateTime MonthToUtc);

internal sealed record RevenueReportBounds(
    DateOnly FromDate, DateOnly ToDate, string TimezoneId,
    DateTime FromUtc, DateTime ToUtc);
