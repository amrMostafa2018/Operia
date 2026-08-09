using MediatR;

namespace Operia.Application.Onboarding.Commands.ExpireTenantSubscriptionIfPastEndDate;

public sealed record ExpireTenantSubscriptionIfPastEndDateCommand : IRequest;
