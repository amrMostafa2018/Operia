using MediatR;
using Operia.Application.Common.Interfaces;
namespace Operia.Application.Admin.Commands.ActivateSubscription;
public sealed class ActivateSubscriptionCommandHandler : IRequestHandler<ActivateSubscriptionCommand>
{
    private readonly ISubscriptionService _subscriptionService;

    public ActivateSubscriptionCommandHandler(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    public Task Handle(ActivateSubscriptionCommand request, CancellationToken cancellationToken)
        => _subscriptionService.ActivateSubscriptionAsync(request.SubscriptionId, cancellationToken);
}