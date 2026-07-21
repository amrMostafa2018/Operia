using MediatR;

namespace Operia.Application.Finance.Queries.ExportTenantSubscriptions;

public sealed record ExportTenantSubscriptionsQuery(
    DateOnly? DateFrom,
    DateOnly? DateTo,
    string? PlanCode,
    string? Status) : IRequest<byte[]>;
