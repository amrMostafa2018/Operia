using MediatR;
using Operia.Application.Onboarding.DTOs;

namespace Operia.Application.Onboarding.Queries.GetSubscriptionPlans;

public sealed record GetSubscriptionPlansQuery : IRequest<IReadOnlyList<SubscriptionPlanDto>>;
