using Operia.Application.Finance.DTOs;
using Operia.Domain.Enums;
using Operia.SharedKernel.Pagination;

namespace Operia.Application.Common.Interfaces;

public interface IFinanceService
{
    Task<PagedList<TenantSubscriptionDto>> GetTenantSubscriptionsAsync(
        string tenantId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? planCode,
        SubscriptionStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportTenantSubscriptionsAsync(
        string tenantId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? planCode,
        SubscriptionStatus? status,
        CancellationToken cancellationToken = default);
}
