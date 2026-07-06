using MediatR;
using Operia.Application.Common.Interfaces;

namespace Operia.Application.Admin.Commands.AddTenantBalance;

public sealed class AddTenantBalanceCommandHandler : IRequestHandler<AddTenantBalanceCommand>
{
    private readonly IAdminTenantService _adminTenantService;

    public AddTenantBalanceCommandHandler(IAdminTenantService adminTenantService)
    {
        _adminTenantService = adminTenantService;
    }

    public Task Handle(AddTenantBalanceCommand request, CancellationToken cancellationToken)
        => _adminTenantService.AddTenantBalanceAsync(request.TenantId, request.Amount, cancellationToken);
}
