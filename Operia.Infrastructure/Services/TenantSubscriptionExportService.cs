using Operia.Application.Common.Interfaces;
using Operia.Application.Common.Models;
using Operia.Application.Finance.Export;
using Operia.Application.Finance.Mapping;
using Operia.Application.Finance.Resources;
using Operia.Domain.Enums;
using Operia.Domain.Interfaces;

namespace Operia.Infrastructure.Services;

public sealed class TenantSubscriptionExportService : ITenantSubscriptionExportService
{
    private readonly ITenantSubscriptionRepository _tenantSubscriptionRepository;
    private readonly ITenantSubscriptionsExcelExporter _excelExporter;
    private readonly ITenantSubscriptionsPdfExporter _pdfExporter;

    public TenantSubscriptionExportService(
        ITenantSubscriptionRepository tenantSubscriptionRepository,
        ITenantSubscriptionsExcelExporter excelExporter,
        ITenantSubscriptionsPdfExporter pdfExporter)
    {
        _tenantSubscriptionRepository = tenantSubscriptionRepository;
        _excelExporter = excelExporter;
        _pdfExporter = pdfExporter;
    }

    public async Task<FileExportResult> ExportTenantSubscriptionsAsync(
        string tenantId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? planCode,
        SubscriptionStatus? status,
        string language,
        string format = "excel",
        CancellationToken cancellationToken = default)
    {
        var subscriptions = await _tenantSubscriptionRepository.GetFilteredByTenantAsync(
            tenantId,
            dateFrom,
            dateTo,
            planCode,
            status,
            cancellationToken);

        var financeResources = FinanceResourceLocalizer.GetResources(language);
        var dtos = TenantSubscriptionMapper.ToDtos(subscriptions);
        var content = format.Trim().Equals("pdf", StringComparison.OrdinalIgnoreCase)
            ? _pdfExporter.Export(dtos, financeResources)
            : _excelExporter.Export(dtos, financeResources);

        return TenantSubscriptionExportFileFactory.Create(content, format);
    }
}
