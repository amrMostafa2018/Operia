using MediatR;

namespace Operia.Application.Onboarding.Commands.ActivateSubscription;

public sealed record ActivateSubscriptionCommand(string SubscriptionId) : IRequest;
