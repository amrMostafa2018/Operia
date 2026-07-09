using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.Application.Onboarding.DTOs;

namespace Operia.Application.Onboarding.Commands.SetupBusiness;

public sealed class SetupBusinessCommandHandler
    : IRequestHandler<SetupBusinessCommand, SetupBusinessResultDto>
{
    private readonly IOnboardingService _onboardingService;
    private readonly ICurrentUserService _currentUserService;

    public SetupBusinessCommandHandler(
        IOnboardingService onboardingService,
        ICurrentUserService currentUserService)
    {
        _onboardingService = onboardingService;
        _currentUserService = currentUserService;
    }

    public Task<SetupBusinessResultDto> Handle(
        SetupBusinessCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException();

        return _onboardingService.SetupBusinessAsync(
            userId,
            request.BusinessName,
            request.BusinessType,
            request.CountryCode,
            request.City,
            request.CurrencyCode,
            request.LogoUrl,
            request.PredeterminedTenantId,
            cancellationToken);
    }
}
