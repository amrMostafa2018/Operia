using MediatR;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Finance.Helpers;

namespace Operia.Application.Finance.Queries.ExportTenantSubscriptions;

public sealed class ExportTenantSubscriptionsQueryHandler
    : IRequestHandler<ExportTenantSubscriptionsQuery, byte[]>
{
    private readonly IFinanceService _financeService;
    private readonly ICurrentUserService _currentUserService;

    public ExportTenantSubscriptionsQueryHandler(
        IFinanceService financeService,
        ICurrentUserService currentUserService)
    {
        _financeService = financeService;
        _currentUserService = currentUserService;
    }

    public Task<byte[]> Handle(
        ExportTenantSubscriptionsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId
            ?? throw new UnauthorizedException("Tenant context is required.");

        return _financeService.ExportTenantSubscriptionsAsync(
            tenantId,
            request.DateFrom,
            request.DateTo,
            request.PlanCode,
            FinanceFilterMapper.ParseStatus(request.Status),
            request.Language,
            cancellationToken);
    }
}
