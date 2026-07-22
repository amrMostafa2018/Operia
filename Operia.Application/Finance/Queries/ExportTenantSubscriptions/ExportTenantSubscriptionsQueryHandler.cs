using MediatR;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Common.Models;
using Operia.Application.Finance.Helpers;

namespace Operia.Application.Finance.Queries.ExportTenantSubscriptions;

public sealed class ExportTenantSubscriptionsQueryHandler
    : IRequestHandler<ExportTenantSubscriptionsQuery, FileExportResult>
{
    private readonly ITenantSubscriptionExportService _tenantSubscriptionExportService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IRequestLanguageService _requestLanguageService;

    public ExportTenantSubscriptionsQueryHandler(
        ITenantSubscriptionExportService tenantSubscriptionExportService,
        ICurrentUserService currentUserService,
        IRequestLanguageService requestLanguageService)
    {
        _tenantSubscriptionExportService = tenantSubscriptionExportService;
        _currentUserService = currentUserService;
        _requestLanguageService = requestLanguageService;
    }

    public Task<FileExportResult> Handle(
        ExportTenantSubscriptionsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId
            ?? throw new UnauthorizedException("Tenant context is required.");

        return _tenantSubscriptionExportService.ExportTenantSubscriptionsAsync(
            tenantId,
            request.DateFrom,
            request.DateTo,
            request.PlanCode,
            FinanceFilterMapper.ParseStatus(request.Status),
            _requestLanguageService.GetLanguage(),
            request.Format,
            cancellationToken);
    }
}
