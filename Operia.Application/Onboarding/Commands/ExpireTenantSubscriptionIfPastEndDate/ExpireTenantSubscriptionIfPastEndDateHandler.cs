using MediatR;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Onboarding.Common;
using Operia.Domain.Enums;
using Operia.Domain.Interfaces;
using Operia.SharedKernel.Interfaces;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Onboarding.Commands.ExpireTenantSubscriptionIfPastEndDate;

public sealed class ExpireTenantSubscriptionIfPastEndDateHandler
    : IRequestHandler<ExpireTenantSubscriptionIfPastEndDateCommand>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantSubscriptionRepository _tenantSubscriptionRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IApplicationDbContext _dbContext;

    public ExpireTenantSubscriptionIfPastEndDateHandler(
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

    public async Task Handle(
        ExpireTenantSubscriptionIfPastEndDateCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw UnauthorizedException.FromCode(ApiErrorCodes.Auth.AuthenticationRequired, "detail");

        var tenant = await OnboardingHandlerHelpers.ResolveTenantForStatusAsync(
            _tenantRepository,
            userId,
            _currentUserService.TenantId,
            cancellationToken);

        if (tenant is null)
            return;

        var subscription = await _tenantSubscriptionRepository.GetLastByTenantIdAsync(
            tenant.Id,
            cancellationToken);

        if (subscription is null || subscription.Status != SubscriptionStatus.Active)
            return;

        var today = DateOnly.FromDateTime(_dateTimeProvider.UtcNow);
        if (!OnboardingHandlerHelpers.IsPastEndDate(subscription, today))
            return;

        var trackedSubscription = await _tenantSubscriptionRepository.GetByIdWithDetailsAsync(
            subscription.Id,
            cancellationToken);

        if (trackedSubscription is null)
            return;

        trackedSubscription.Status = SubscriptionStatus.Expired;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
