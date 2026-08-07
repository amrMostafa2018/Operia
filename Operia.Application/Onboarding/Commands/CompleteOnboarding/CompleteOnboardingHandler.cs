using MediatR;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Onboarding.DTOs;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Domain.Exceptions;
using Operia.Domain.Interfaces;
using Operia.SharedKernel.Interfaces;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Onboarding.Commands.CompleteOnboarding;

public sealed class CompleteOnboardingHandler
    : IRequestHandler<CompleteOnboardingCommand, OnboardingResultDto>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ISubscriptionPlanRepository _subscriptionPlanRepository;
    private readonly ITenantSubscriptionRepository _tenantSubscriptionRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public CompleteOnboardingHandler(
        ITenantRepository tenantRepository,
        ISubscriptionPlanRepository subscriptionPlanRepository,
        ITenantSubscriptionRepository tenantSubscriptionRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _subscriptionPlanRepository = subscriptionPlanRepository;
        _tenantSubscriptionRepository = tenantSubscriptionRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<OnboardingResultDto> Handle(
        CompleteOnboardingCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw UnauthorizedException.FromCode(ApiErrorCodes.Auth.AuthenticationRequired, "detail");

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

        var plan = await _subscriptionPlanRepository.GetActiveByIdAsync(request.PlanId, cancellationToken)
            ?? throw new NotFoundException(nameof(SubscriptionPlan), request.PlanId);

        var amount = request.BillingType == BillingType.Monthly
            ? plan.MonthlyPrice
            : plan.YearlyPrice;

        var subscription = new TenantSubscription
        {
            TenantId = tenant.Id,
            PlanId = plan.Id,
            Amount = amount,
            Currency = tenant.CurrencyCode,
            BillingType = request.BillingType,
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
}
