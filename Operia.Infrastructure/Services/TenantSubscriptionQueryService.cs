using Operia.Application.Common.Interfaces;
using Operia.Application.Finance.DTOs;
using Operia.Application.Finance.Mapping;
using Operia.Domain.Enums;
using Operia.Domain.Interfaces;
using Operia.SharedKernel.Pagination;

namespace Operia.Infrastructure.Services;

public sealed class TenantSubscriptionQueryService : ITenantSubscriptionQueryService
{
    private readonly ITenantSubscriptionRepository _tenantSubscriptionRepository;

    public TenantSubscriptionQueryService(ITenantSubscriptionRepository tenantSubscriptionRepository)
    {
        _tenantSubscriptionRepository = tenantSubscriptionRepository;
    }

    public async Task<PagedList<TenantSubscriptionDto>> GetTenantSubscriptionsAsync(
        string tenantId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? planCode,
        SubscriptionStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _tenantSubscriptionRepository.GetFilteredByTenantPagedAsync(
            tenantId,
            dateFrom,
            dateTo,
            planCode,
            status,
            pageNumber,
            pageSize,
            cancellationToken);

        var dtos = TenantSubscriptionMapper.ToDtos(items);
        return PagedList<TenantSubscriptionDto>.FromItems(dtos, pageNumber, pageSize, totalCount);
    }
}
