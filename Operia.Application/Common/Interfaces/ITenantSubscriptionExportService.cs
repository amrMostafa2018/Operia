using Operia.Application.Common.Models;
using Operia.Domain.Enums;

namespace Operia.Application.Common.Interfaces;

public interface ITenantSubscriptionExportService
{
    Task<FileExportResult> ExportTenantSubscriptionsAsync(
        string tenantId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? planCode,
        SubscriptionStatus? status,
        string language,
        string format = "excel",
        CancellationToken cancellationToken = default);
}
