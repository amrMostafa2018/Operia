using MediatR;
using Operia.Application.Common.Interfaces;

namespace Operia.Application.Admin.Commands.ApproveAddBalancePlatform;

public sealed class ApproveAddBalancePlatformCommandHandler : IRequestHandler<ApproveAddBalancePlatformCommand>
{
    private readonly IPlatformService _platformService;

    public ApproveAddBalancePlatformCommandHandler(IPlatformService platformService)
    {
        _platformService = platformService;
    }

    public Task Handle(ApproveAddBalancePlatformCommand request, CancellationToken cancellationToken)
        => _platformService.ApproveAddBalancePlatformAsync(request.RevenueId, cancellationToken);
}
