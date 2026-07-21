using MediatR;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Finance.DTOs;
using Operia.Application.Finance.Helpers;
using Operia.SharedKernel.Pagination;

namespace Operia.Application.Finance.Queries.GetTenantSubscriptions;

public sealed class GetTenantSubscriptionsQueryHandler
    : IRequestHandler<GetTenantSubscriptionsQuery, PagedList<TenantSubscriptionDto>>
{
    private readonly IFinanceService _financeService;
    private readonly ICurrentUserService _currentUserService;

    public GetTenantSubscriptionsQueryHandler(
        IFinanceService financeService,
        ICurrentUserService currentUserService)
    {
        _financeService = financeService;
        _currentUserService = currentUserService;
    }

    public Task<PagedList<TenantSubscriptionDto>> Handle(
        GetTenantSubscriptionsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId
            ?? throw new UnauthorizedException("Tenant context is required.");

        return _financeService.GetTenantSubscriptionsAsync(
            tenantId,
            request.DateFrom,
            request.DateTo,
            request.PlanCode,
            FinanceFilterMapper.ParseStatus(request.Status),
            request.PageNumber,
            request.PageSize,
            cancellationToken);
    }
}
