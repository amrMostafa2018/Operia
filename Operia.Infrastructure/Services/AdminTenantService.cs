using FluentValidation.Results;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Domain.Exceptions;
using Operia.Domain.Interfaces;
using Operia.SharedKernel.Interfaces;

namespace Operia.Infrastructure.Services;

public sealed class AdminTenantService : IAdminTenantService
{
    private readonly ITenantSubscriptionRepository _tenantSubscriptionRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IPlatformRevenueRepository _platformRevenueRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public AdminTenantService(
        ITenantSubscriptionRepository tenantSubscriptionRepository,
        ITenantRepository tenantRepository,
        IPlatformRevenueRepository platformRevenueRepository,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _tenantSubscriptionRepository = tenantSubscriptionRepository;
        _tenantRepository = tenantRepository;
        _platformRevenueRepository = platformRevenueRepository;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task ActivateSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _tenantSubscriptionRepository.GetByIdWithDetailsAsync(
            subscriptionId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(TenantSubscription), subscriptionId);

        if (subscription.Status == SubscriptionStatus.Active)
            return;

        var tenant = subscription.Tenant
            ?? throw new NotFoundException(nameof(Tenant), subscription.TenantId);

        if (tenant.Balance < subscription.Amount)
        {
            throw new ValidationException(
            [
                new ValidationFailure(
                    "balance",
                    "Insufficient tenant balance to activate subscription.")
            ]);
        }

        tenant.Balance -= subscription.Amount;

        var today = DateOnly.FromDateTime(_dateTimeProvider.UtcNow);
        subscription.Status = SubscriptionStatus.Active;
        subscription.StartDate = today;
        subscription.EndDate = subscription.BillingType == BillingType.Monthly
            ? today.AddMonths(1)
            : today.AddYears(1);

        if (subscription.Plan?.TrialDays > 0 && subscription.Amount == 0)
        {
            subscription.EndDate = today.AddDays(subscription.Plan.TrialDays);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ApproveAddBalancePlatformAsync(
        string revenueId,
        CancellationToken cancellationToken = default)
    {
        var revenue = await _platformRevenueRepository.GetByIdAsync(revenueId, cancellationToken)
            ?? throw new NotFoundException(nameof(PlatformRevenue), revenueId);

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
