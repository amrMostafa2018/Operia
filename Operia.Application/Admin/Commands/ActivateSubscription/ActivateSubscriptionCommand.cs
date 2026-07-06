using MediatR;

namespace Operia.Application.Admin.Commands.ActivateSubscription;

public sealed record ActivateSubscriptionCommand(string SubscriptionId) : IRequest;
