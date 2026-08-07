using MediatR;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Onboarding.DTOs;
using Operia.Domain.Enums;
using Operia.Domain.Interfaces;
using Operia.SharedKernel.Interfaces;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Onboarding.Queries.GetOnboardingStatus;

public sealed class GetOnboardingStatusHandler
    : IRequestHandler<GetOnboardingStatusQuery, OnboardingStatusDto>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantSubscriptionRepository _tenantSubscriptionRepository;
    private readonly IPlatformRevenueRepository _platformRevenueRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public GetOnboardingStatusHandler(
        ITenantRepository tenantRepository,
        ITenantSubscriptionRepository tenantSubscriptionRepository,
        IPlatformRevenueRepository platformRevenueRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _tenantSubscriptionRepository = tenantSubscriptionRepository;
        _platformRevenueRepository = platformRevenueRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }


    public async Task<OnboardingStatusDto> Handle(
        GetOnboardingStatusQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw UnauthorizedException.FromCode(ApiErrorCodes.Auth.AuthenticationRequired, "detail");

        var tenantId = _currentUserService.TenantId;

        Operia.Domain.Entities.Tenant? tenant = null;
        if (!string.IsNullOrEmpty(tenantId))
        {
            tenant = await _tenantRepository.GetByIdForStatusAsync(tenantId, cancellationToken);
        }

        if (tenant is null)
        {
            tenant = await _tenantRepository.GetByOwnerUserIdForStatusAsync(userId, cancellationToken);
        }

        if (tenant is null)
        {
            return BuildStatusDto(
                OnboardingStep.Setup, null, null, null, null, 0, 0, null, null);
        }

        var business = tenant.Businesses.FirstOrDefault();
        var businessSummary = business is null
            ? null
            : new BusinessSummaryDto(
                tenant.BusinessName,
                tenant.BusinessType,
                tenant.CountryCode,
                tenant.City,
                tenant.CurrencyCode);

        var pendingAddBalancePlatform = await MapPendingAddBalancePlatformAsync(tenant.Id, cancellationToken);
        var (usableBalance, totalBalance) = MapBalances(tenant.Balance);

        var subscription = tenant.Subscriptions
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefault();

        if (subscription is null)
        {
            return BuildStatusDto(
                OnboardingStep.Plan,
                tenant.Id,
                business?.Id,
                null,
                businessSummary,
                usableBalance,
                totalBalance,
                null,
                pendingAddBalancePlatform);
        }

        if (subscription.Status == SubscriptionStatus.Active)
        {
            var today = DateOnly.FromDateTime(_dateTimeProvider.UtcNow);
            if (subscription.EndDate.HasValue && subscription.EndDate.Value < today)
            {
                var trackedSubscription = await _tenantSubscriptionRepository.GetByIdWithDetailsAsync(
                    subscription.Id,
                    cancellationToken);

                if (trackedSubscription is not null)
                {
                    trackedSubscription.Status = SubscriptionStatus.Expired;
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }

                return BuildStatusDto(
                    OnboardingStep.Plan,
                    tenant.Id,
                    business?.Id,
                    subscription.Id,
                    businessSummary,
                    usableBalance,
                    totalBalance,
                    subscription.Amount,
                    pendingAddBalancePlatform);
            }

            return BuildStatusDto(
                OnboardingStep.Active,
                tenant.Id,
                business?.Id,
                subscription.Id,
                businessSummary,
                usableBalance,
                totalBalance,
                subscription.Amount,
                pendingAddBalancePlatform);
        }

        if (subscription.Status == SubscriptionStatus.Pending)
        {
            return BuildStatusDto(
                OnboardingStep.Plan,
                tenant.Id,
                business?.Id,
                subscription.Id,
                businessSummary,
                usableBalance,
                totalBalance,
                subscription.Amount,
                pendingAddBalancePlatform);
        }

        return BuildStatusDto(
            OnboardingStep.Plan,
            tenant.Id,
            business?.Id,
            subscription.Id,
            businessSummary,
            usableBalance,
            totalBalance,
            subscription.Amount,
            pendingAddBalancePlatform);
    }

    private async Task<PendingAddBalancePlatformDto?> MapPendingAddBalancePlatformAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        var pending = await _platformRevenueRepository.GetLatestPendingAddBalancePlatformByTenantIdAsync(
            tenantId,
            cancellationToken);

        if (pending is null)
        {
            return null;
        }

        return new PendingAddBalancePlatformDto(
            pending.Id,
            pending.Amount,
            pending.ScreenShotUrl);
    }

    private static (decimal UsableBalance, decimal TotalBalance) MapBalances(decimal tenantBalance) =>
        (tenantBalance, tenantBalance);

    private static OnboardingStatusDto BuildStatusDto(
        OnboardingStep step,
        string? tenantId,
        string? businessId,
        string? subscriptionId,
        BusinessSummaryDto? business,
        decimal usableBalance,
        decimal totalBalance,
        decimal? subscriptionAmount,
        PendingAddBalancePlatformDto? pendingAddBalancePlatform) =>
        new(
            step,
            tenantId,
            businessId,
            subscriptionId,
            business,
            usableBalance,
            totalBalance,
            subscriptionAmount,
            pendingAddBalancePlatform);
}
