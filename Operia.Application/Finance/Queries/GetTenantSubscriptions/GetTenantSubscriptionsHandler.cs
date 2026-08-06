using MediatR;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Finance.DTOs;
using Operia.Application.Finance.Helpers;
using Operia.Application.Finance.Mapping;
using Operia.Domain.Interfaces;
using Operia.SharedKernel.Pagination;

namespace Operia.Application.Finance.Queries.GetTenantSubscriptions;

public sealed class GetTenantSubscriptionsHandler
    : IRequestHandler<GetTenantSubscriptionsQuery, PagedList<TenantSubscriptionDto>>
{
    private readonly ITenantSubscriptionRepository _tenantSubscriptionRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetTenantSubscriptionsHandler(
        ITenantSubscriptionRepository tenantSubscriptionRepository,
        ICurrentUserService currentUserService)
    {
        _tenantSubscriptionRepository = tenantSubscriptionRepository;
        _currentUserService = currentUserService;
    }

    public async Task<PagedList<TenantSubscriptionDto>> Handle(
        GetTenantSubscriptionsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId
            ?? throw new UnauthorizedException("Tenant context is required.");

        var (items, totalCount) = await _tenantSubscriptionRepository.GetFilteredByTenantPagedAsync(
            tenantId,
            request.DateFrom,
            request.DateTo,
            request.PlanCode,
            FinanceFilterMapper.ParseStatus(request.Status),
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var dtos = TenantSubscriptionMapper.ToDtos(items);
        return PagedList<TenantSubscriptionDto>.FromItems(dtos, request.PageNumber, request.PageSize, totalCount);
    }
}
