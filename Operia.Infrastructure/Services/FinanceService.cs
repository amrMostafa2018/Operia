using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Interfaces;
using Operia.Application.Finance.DTOs;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Pagination;

namespace Operia.Infrastructure.Services;

public sealed class FinanceService : IFinanceService
{
    private readonly ApplicationDbContext _context;

    public FinanceService(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<PagedList<TenantSubscriptionDto>> GetTenantSubscriptionsAsync(
        string tenantId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? planCode,
        SubscriptionStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
        => GetSubscriptionsPagedAsync(
            tenantId,
            dateFrom,
            dateTo,
            planCode,
            status,
            pageNumber,
            pageSize,
            cancellationToken);

    public async Task<byte[]> ExportTenantSubscriptionsAsync(
        string tenantId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? planCode,
        SubscriptionStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = BuildFilteredQuery(
            tenantId,
            dateFrom,
            dateTo,
            planCode,
            status);

        var subscriptions = await query.ToListAsync(cancellationToken);
        var builder = new StringBuilder();
        builder.AppendLine("Plan,PlanCode,BillingType,Amount,Currency,StartDate,EndDate,Status");

        foreach (var subscription in subscriptions)
        {
            var dto = MapToDto(subscription);
            builder.AppendLine(string.Join(',',
                EscapeCsv(dto.PlanName),
                EscapeCsv(dto.PlanCode),
                EscapeCsv(dto.BillingType),
                dto.Amount.ToString(CultureInfo.InvariantCulture),
                EscapeCsv(dto.Currency),
                FormatDate(dto.StartDate),
                FormatDate(dto.EndDate),
                EscapeCsv(dto.Status)));
        }

        return Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(builder.ToString()))
            .ToArray();
    }

    private async Task<PagedList<TenantSubscriptionDto>> GetSubscriptionsPagedAsync(
        string tenantId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? planCode,
        SubscriptionStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = BuildFilteredQuery(
            tenantId,
            dateFrom,
            dateTo,
            planCode,
            status);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(MapToDto).ToList();
        return PagedList<TenantSubscriptionDto>.FromItems(dtos, pageNumber, pageSize, totalCount);
    }

    private IQueryable<TenantSubscription> BuildFilteredQuery(
        string tenantId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? planCode,
        SubscriptionStatus? status)
    {
        var query = _context.TenantSubscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .Where(s => s.TenantId == tenantId);

        if (dateFrom.HasValue)
        {
            query = query.Where(s => s.StartDate >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(s => s.StartDate <= dateTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(planCode))
        {
            query = query.Where(s => s.Plan != null && s.Plan.Code == planCode);
        }

        if (status.HasValue)
        {
            query = query.Where(s => s.Status == status.Value);
        }

        return query.OrderByDescending(s => s.StartDate ?? DateOnly.MinValue)
            .ThenByDescending(s => s.CreatedAt);
    }

    private static TenantSubscriptionDto MapToDto(TenantSubscription subscription) =>
        new(
            subscription.Id,
            subscription.Plan?.Code ?? string.Empty,
            subscription.Plan?.Name ?? string.Empty,
            MapBillingType(subscription.BillingType),
            subscription.Amount,
            subscription.Currency,
            subscription.StartDate,
            subscription.EndDate,
            MapStatus(subscription.Status));

    private static string MapBillingType(BillingType billingType) =>
        billingType switch
        {
            BillingType.Monthly => "monthly",
            BillingType.Yearly => "yearly",
            _ => billingType.ToString().ToLowerInvariant()
        };

    private static string MapStatus(SubscriptionStatus status) =>
        status switch
        {
            SubscriptionStatus.Active => "active",
            SubscriptionStatus.Expired => "expired",
            SubscriptionStatus.Cancelled => "cancelled",
            SubscriptionStatus.Pending => "pending",
            _ => status.ToString().ToLowerInvariant()
        };

    private static string FormatDate(DateOnly? date) =>
        date?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }
}
