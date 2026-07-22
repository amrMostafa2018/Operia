using System.Text.Json;
using FluentValidation.Results;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Onboarding.DTOs;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Domain.Exceptions;
using Operia.Domain.Interfaces;
using Operia.SharedKernel.Interfaces;

namespace Operia.Infrastructure.Services;

public sealed class SubscriptionService : ISubscriptionService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ISubscriptionPlanRepository _subscriptionPlanRepository;
    private readonly ITenantSubscriptionRepository _tenantSubscriptionRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public SubscriptionService(
        ITenantRepository tenantRepository,
        ISubscriptionPlanRepository subscriptionPlanRepository,
        ITenantSubscriptionRepository tenantSubscriptionRepository,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _subscriptionPlanRepository = subscriptionPlanRepository;
        _tenantSubscriptionRepository = tenantSubscriptionRepository;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<SubscriptionPlanDto>> GetActivePlansAsync(
        CancellationToken cancellationToken = default)
    {
        var plans = await _subscriptionPlanRepository.GetAllActiveAsync(cancellationToken);

        return plans.Select(plan => new SubscriptionPlanDto(
            plan.Id,
            plan.Name,
            plan.Code,
            plan.MonthlyPrice,
            plan.YearlyPrice,
            plan.TrialDays,
            ParseFeatures(plan.FeaturesJson),
            plan.IsActive)).ToList();
    }

    public async Task<OnboardingResultDto> SelectPlanAsync(
        string userId,
        string planId,
        BillingType billingType,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByOwnerUserIdWithDetailsAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), userId);

        var business = tenant.Businesses.FirstOrDefault()
            ?? throw new NotFoundException(nameof(Business), tenant.Id);

        var pendingSubscription = tenant.Subscriptions
            .FirstOrDefault(s => s.Status == SubscriptionStatus.Pending);

        if (pendingSubscription is not null)
        {
            return new OnboardingResultDto(
                tenant.Id,
                business.Id,
                pendingSubscription.Id,
                pendingSubscription.Status.ToString());
        }

        var plan = await _subscriptionPlanRepository.GetActiveByIdAsync(planId, cancellationToken)
            ?? throw new NotFoundException(nameof(SubscriptionPlan), planId);

        var amount = billingType == BillingType.Monthly
            ? plan.MonthlyPrice
            : plan.YearlyPrice;

        var subscription = new TenantSubscription
        {
            TenantId = tenant.Id,
            PlanId = plan.Id,
            Amount = amount,
            Currency = tenant.CurrencyCode,
            BillingType = billingType,
            Status = SubscriptionStatus.Pending
        };

        await _tenantSubscriptionRepository.AddAsync(subscription, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new OnboardingResultDto(
            tenant.Id,
            business.Id,
            subscription.Id,
            subscription.Status.ToString());
    }

    public async Task ActivateSubscriptionAsync(
        string userId,
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByOwnerUserIdForStatusAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), userId);

        var ownsSubscription = tenant.Subscriptions.Any(s => s.Id == subscriptionId);
        if (!ownsSubscription)
        {
            throw new NotFoundException(nameof(TenantSubscription), subscriptionId);
        }

        await ActivateSubscriptionAsync(subscriptionId, cancellationToken);
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

    private static IReadOnlyList<string> ParseFeatures(string featuresJson)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(featuresJson) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
