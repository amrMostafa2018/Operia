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

public sealed class PlatformService : IPlatformService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IPlatformRevenueRepository _platformRevenueRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public PlatformService(
        ITenantRepository tenantRepository,
        IPlatformRevenueRepository platformRevenueRepository,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _platformRevenueRepository = platformRevenueRepository;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<AddBalancePlatformResultDto> AddBalancePlatformAsync(
        string userId,
        decimal amount,
        string screenShotUrl,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByOwnerUserIdForStatusAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), userId);

        var existingPending = await _platformRevenueRepository.GetLatestPendingAddBalancePlatformByTenantIdAsync(
            tenant.Id,
            cancellationToken);

        if (existingPending is not null)
        {
            throw new ValidationException(
            [
                new ValidationFailure(
                    "addBalancePlatform",
                    "A balance add request is already pending review.")
            ]);
        }

        BalancePlatformValidation.EnsureScreenshotProvided(
            screenShotUrl,
            "Balance add request must include an Instapay screenshot.");

        var revenue = new PlatformRevenue
        {
            TenantId = tenant.Id,
            Amount = amount,
            Currency = tenant.CurrencyCode,
            ScreenShotUrl = screenShotUrl,
            Status = PlatformRevenueStatus.Pending,
            RecordedAt = _dateTimeProvider.UtcNow
        };

        await _platformRevenueRepository.AddAsync(revenue, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AddBalancePlatformResultDto(revenue.Id, revenue.Amount);
    }

    public async Task ApproveAddBalancePlatformAsync(
        string revenueId,
        CancellationToken cancellationToken = default)
    {
        var revenue = await _platformRevenueRepository.GetByIdAsync(revenueId, cancellationToken)
            ?? throw new NotFoundException(nameof(PlatformRevenue), revenueId);

        if (revenue.Status == PlatformRevenueStatus.Confirmed)
            return;

        BalancePlatformValidation.EnsureScreenshotProvided(
            revenue.ScreenShotUrl,
            "Balance top-up request must include an Instapay screenshot.");

        var tenant = await _tenantRepository.GetByIdAsync(revenue.TenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), revenue.TenantId);

        tenant.Balance += revenue.Amount;
        revenue.Status = PlatformRevenueStatus.Confirmed;
        revenue.ConfirmedAt = _dateTimeProvider.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<PendingAddBalancePlatformDto?> GetPendingAddBalancePlatformAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
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
}
