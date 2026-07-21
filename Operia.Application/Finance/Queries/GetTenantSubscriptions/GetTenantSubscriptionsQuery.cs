using MediatR;
using Operia.Application.Finance.DTOs;
using Operia.SharedKernel.Pagination;

namespace Operia.Application.Finance.Queries.GetTenantSubscriptions;

public sealed record GetTenantSubscriptionsQuery(
    DateOnly? DateFrom,
    DateOnly? DateTo,
    string? PlanCode,
    string? Status,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<PagedList<TenantSubscriptionDto>>;
