using FluentValidation.Results;
using MediatR;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Domain.Exceptions;
using Operia.Domain.Interfaces;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Admin.Commands.ApproveAddBalancePlatform;

public sealed class ApproveAddBalancePlatformHandler : IRequestHandler<ApproveAddBalancePlatformCommand>
{
    private readonly IPlatformRevenueRepository _platformRevenueRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveAddBalancePlatformHandler(
        IPlatformRevenueRepository platformRevenueRepository,
        ITenantRepository tenantRepository,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _platformRevenueRepository = platformRevenueRepository;
        _tenantRepository = tenantRepository;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(ApproveAddBalancePlatformCommand request, CancellationToken cancellationToken)
    {
        var revenue = await _platformRevenueRepository.GetByIdAsync(request.RevenueId, cancellationToken)
            ?? throw new NotFoundException(nameof(PlatformRevenue), request.RevenueId);

        if (revenue.Status == PlatformRevenueStatus.Confirmed)
            return;

        if (string.IsNullOrWhiteSpace(revenue.ScreenShotUrl))
        {
            throw new ValidationException(
            [
                new ValidationFailure(
                    "screenShotUrl",
                    "Balance top-up request must include an Instapay screenshot.")
            ]);
        }

        var tenant = await _tenantRepository.GetByIdAsync(revenue.TenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), revenue.TenantId);

        tenant.Balance += revenue.Amount;
        revenue.Status = PlatformRevenueStatus.Confirmed;
        revenue.ConfirmedAt = _dateTimeProvider.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
