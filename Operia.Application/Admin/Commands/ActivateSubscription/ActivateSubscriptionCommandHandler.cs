using MediatR;
using Operia.Application.Common.Interfaces;
namespace Operia.Application.Admin.Commands.ActivateSubscription;
public sealed class ActivateSubscriptionCommandHandler : IRequestHandler<ActivateSubscriptionCommand>
{
    private readonly IAdminTenantService _adminTenantService;
    public ActivateSubscriptionCommandHandler(IAdminTenantService adminTenantService)
    {
        _adminTenantService = adminTenantService;
    }
    public Task Handle(ActivateSubscriptionCommand request, CancellationToken cancellationToken)
        => _adminTenantService.ActivateSubscriptionAsync(request.SubscriptionId, cancellationToken);
}