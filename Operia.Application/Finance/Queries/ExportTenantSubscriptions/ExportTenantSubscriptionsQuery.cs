using MediatR;
using Operia.Application.Common.Models;

namespace Operia.Application.Finance.Queries.ExportTenantSubscriptions;

public sealed record ExportTenantSubscriptionsQuery(
    DateOnly? DateFrom,
    DateOnly? DateTo,
    string? PlanCode,
    string? Status,
    string Format = "excel") : IRequest<FileExportResult>;
