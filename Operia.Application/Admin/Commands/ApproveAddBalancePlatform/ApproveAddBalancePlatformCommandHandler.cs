using MediatR;
using Operia.Application.Common.Interfaces;

namespace Operia.Application.Admin.Commands.ApproveAddBalancePlatform;

public sealed class ApproveAddBalancePlatformCommandHandler : IRequestHandler<ApproveAddBalancePlatformCommand>
{
    private readonly IAdminTenantService _adminTenantService;

    public ApproveAddBalancePlatformCommandHandler(IAdminTenantService adminTenantService)
    {
        _adminTenantService = adminTenantService;
    }

    public Task Handle(ApproveAddBalancePlatformCommand request, CancellationToken cancellationToken)
        => _adminTenantService.ApproveAddBalancePlatformAsync(request.RevenueId, cancellationToken);
}
