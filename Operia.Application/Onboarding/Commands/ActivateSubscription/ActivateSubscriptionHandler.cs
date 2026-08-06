using FluentValidation.Results;
using MediatR;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Domain.Exceptions;
using Operia.Domain.Interfaces;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Onboarding.Commands.ActivateSubscription;

public sealed class ActivateSubscriptionHandler : IRequestHandler<ActivateSubscriptionCommand>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantSubscriptionRepository _tenantSubscriptionRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public ActivateSubscriptionHandler(
        ITenantRepository tenantRepository,
        ITenantSubscriptionRepository tenantSubscriptionRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _tenantSubscriptionRepository = tenantSubscriptionRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(ActivateSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException();

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
}
