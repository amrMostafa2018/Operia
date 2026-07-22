using MediatR;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Common.Models;
using Operia.Application.Finance.Export;
using Operia.Application.Finance.Helpers;
using Operia.Application.Finance.Mapping;
using Operia.Application.Finance.Resources;
using Operia.Domain.Interfaces;

namespace Operia.Application.Finance.Queries.ExportTenantSubscriptions;

public sealed class ExportTenantSubscriptionsQueryHandler
    : IRequestHandler<ExportTenantSubscriptionsQuery, FileExportResult>
{
    private readonly ITenantSubscriptionRepository _tenantSubscriptionRepository;
    private readonly ITenantSubscriptionsExcelExporter _excelExporter;
    private readonly ITenantSubscriptionsPdfExporter _pdfExporter;
    private readonly ICurrentUserService _currentUserService;
    private readonly IRequestLanguageService _requestLanguageService;

    public ExportTenantSubscriptionsQueryHandler(
        ITenantSubscriptionRepository tenantSubscriptionRepository,
        ITenantSubscriptionsExcelExporter excelExporter,
        ITenantSubscriptionsPdfExporter pdfExporter,
        ICurrentUserService currentUserService,
        IRequestLanguageService requestLanguageService)
    {
        _tenantSubscriptionRepository = tenantSubscriptionRepository;
        _excelExporter = excelExporter;
        _pdfExporter = pdfExporter;
        _currentUserService = currentUserService;
        _requestLanguageService = requestLanguageService;
    }

    public async Task<FileExportResult> Handle(
        ExportTenantSubscriptionsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId
            ?? throw new UnauthorizedException("Tenant context is required.");

        var subscriptions = await _tenantSubscriptionRepository.GetFilteredByTenantAsync(
            tenantId,
            request.DateFrom,
            request.DateTo,
            request.PlanCode,
            FinanceFilterMapper.ParseStatus(request.Status),
            cancellationToken);

        var financeResources = FinanceResourceLocalizer.GetResources(_requestLanguageService.GetLanguage());
        var dtos = TenantSubscriptionMapper.ToDtos(subscriptions);
        var content = request.Format.Trim().Equals("pdf", StringComparison.OrdinalIgnoreCase)
            ? _pdfExporter.Export(dtos, financeResources)
            : _excelExporter.Export(dtos, financeResources);

        return TenantSubscriptionExportFileFactory.Create(content, request.Format);
    }
}
