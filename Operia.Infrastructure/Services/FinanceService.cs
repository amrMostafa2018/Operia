using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Finance.Resources;
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
        string language,
        CancellationToken cancellationToken = default)
    {
        var query = BuildFilteredQuery(
            tenantId,
            dateFrom,
            dateTo,
            planCode,
            status);

        var subscriptions = await query.ToListAsync(cancellationToken);
        var financeResources = FinanceResourceLocalizer.GetResources(language);
        var exportLabels = financeResources.SubscriptionsExport;

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(exportLabels.SheetName);
        worksheet.RightToLeft = financeResources.IsArabic;

        for (var column = 0; column < exportLabels.Headers.Length; column++)
        {
            var cell = worksheet.Cell(1, column + 1);
            cell.Value = exportLabels.Headers[column];
            cell.Style.Font.Bold = true;
        }

        var rowIndex = 2;
        foreach (var subscription in subscriptions)
        {
            var dto = MapToDto(subscription);
            worksheet.Cell(rowIndex, 1).Value = dto.PlanName;
            worksheet.Cell(rowIndex, 2).Value = dto.PlanCode;
            worksheet.Cell(rowIndex, 3).Value = financeResources.GetBillingTypeLabel(dto.BillingType);
            worksheet.Cell(rowIndex, 4).Value = dto.Amount;
            worksheet.Cell(rowIndex, 5).Value = financeResources.GetCurrencyLabel(dto.Currency);
            worksheet.Cell(rowIndex, 6).Value = FormatDate(dto.StartDate);
            worksheet.Cell(rowIndex, 7).Value = FormatDate(dto.EndDate);
            worksheet.Cell(rowIndex, 8).Value = financeResources.GetStatusLabel(dto.Status);
            rowIndex++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
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
}
