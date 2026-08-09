using FluentValidation.Results;
using MediatR;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Onboarding.Common;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Domain.Exceptions;
using Operia.Domain.Interfaces;
using Operia.SharedKernel.Interfaces;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Onboarding.Commands.ActivateSubscription;

public sealed class ActivateSubscriptionHandler : IRequestHandler<ActivateSubscriptionCommand>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantSubscriptionRepository _tenantSubscriptionRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IApplicationDbContext _dbContext;

    public ActivateSubscriptionHandler(
        ITenantRepository tenantRepository,
        ITenantSubscriptionRepository tenantSubscriptionRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IApplicationDbContext dbContext)
    {
        _tenantRepository = tenantRepository;
        _tenantSubscriptionRepository = tenantSubscriptionRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _dbContext = dbContext;
    }

    public async Task Handle(ActivateSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw UnauthorizedException.FromCode(ApiErrorCodes.Auth.AuthenticationRequired, "detail");

        var tenantStatus = await _tenantRepository.GetByOwnerUserIdForStatusAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), userId);

        var ownsSubscription = tenantStatus.Subscriptions.Any(s => s.Id == request.SubscriptionId);
        if (!ownsSubscription)
        {
            throw new NotFoundException(nameof(TenantSubscription), request.SubscriptionId);
        }

        var subscription = await _tenantSubscriptionRepository.GetByIdWithDetailsAsync(
            request.SubscriptionId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(TenantSubscription), request.SubscriptionId);

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
        var (startDate, endDate) = OnboardingHandlerHelpers.ComputeActivationPeriod(subscription, today);

        if (subscription.Status == SubscriptionStatus.Expired)
        {
            var renewedSubscription = new TenantSubscription
            {
                TenantId = subscription.TenantId,
                PlanId = subscription.PlanId,
                Amount = subscription.Amount,
                Currency = subscription.Currency,
                BillingType = subscription.BillingType,
                Status = SubscriptionStatus.Active,
                StartDate = startDate,
                EndDate = endDate
            };

            await _tenantSubscriptionRepository.AddAsync(renewedSubscription, cancellationToken);
        }
        else
        {
            subscription.Status = SubscriptionStatus.Active;
            subscription.StartDate = startDate;
            subscription.EndDate = endDate;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
