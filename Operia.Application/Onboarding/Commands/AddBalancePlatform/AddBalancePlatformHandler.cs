using FluentValidation.Results;
using MediatR;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Onboarding.Common;
using Operia.Application.Onboarding.DTOs;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Domain.Exceptions;
using Operia.Domain.Interfaces;
using Operia.SharedKernel.Interfaces;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Onboarding.Commands.AddBalancePlatform;

public sealed class AddBalancePlatformHandler
    : IRequestHandler<AddBalancePlatformCommand, AddBalancePlatformResultDto>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IPlatformRevenueRepository _platformRevenueRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public AddBalancePlatformHandler(
        ITenantRepository tenantRepository,
        IPlatformRevenueRepository platformRevenueRepository,
        ICurrentUserService currentUserService,
        IFileStorageService fileStorageService,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _platformRevenueRepository = platformRevenueRepository;
        _currentUserService = currentUserService;
        _fileStorageService = fileStorageService;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<AddBalancePlatformResultDto> Handle(
        AddBalancePlatformCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw UnauthorizedException.FromCode(ApiErrorCodes.Auth.AuthenticationRequired, "detail");

        await using var screenshot = request.Screenshot!;

        var uploadContext = await OnboardingHandlerHelpers.ResolveUploadTenantContextAsync(
            _tenantRepository,
            userId,
            _currentUserService.TenantId,
            cancellationToken);

        var screenShotUrl = await _fileStorageService.SaveAsync(
            screenshot.Content,
            screenshot.FileName,
            screenshot.ContentType,
            uploadContext.TenantId,
            FileUploadCategory.PlatformRevenue,
            cancellationToken);

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

        if (string.IsNullOrWhiteSpace(screenShotUrl))
        {
            throw new ValidationException(
            [
                new ValidationFailure(
                    "screenShotUrl",
                    "Balance add request must include an Instapay screenshot.")
            ]);
        }

        var revenue = new PlatformRevenue
        {
            TenantId = tenant.Id,
            Amount = request.Amount,
            Currency = tenant.CurrencyCode,
            ScreenShotUrl = screenShotUrl,
            Status = PlatformRevenueStatus.Pending,
            RecordedAt = _dateTimeProvider.UtcNow
        };

        await _platformRevenueRepository.AddAsync(revenue, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AddBalancePlatformResultDto(revenue.Id, revenue.Amount);
    }
}
