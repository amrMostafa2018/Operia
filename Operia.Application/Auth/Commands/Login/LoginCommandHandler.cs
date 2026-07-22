using MediatR;
using Operia.Application.Auth.DTOs;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Common.PhoneNumbers;

namespace Operia.Application.Auth.Commands.Login;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResultDto>
{
    private readonly IIdentityService _identityService;
    private readonly IOtpService _otpService;

    public LoginCommandHandler(IIdentityService identityService, IOtpService otpService)
    {
        _identityService = identityService;
        _otpService = otpService;
    }

    public async Task<LoginResultDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var (succeeded, userId, errors) = await _identityService.ValidateCredentialsAsync(
            PhoneNumberHelper.ToE164(request.PhoneNumber),
            request.Password,
            cancellationToken);

        if (!succeeded)
            throw new UnauthorizedException(errors.FirstOrDefault() ?? "Invalid phone number or password.");

        await _otpService.GenerateAndSendOtpAsync(userId, cancellationToken);

        return new LoginResultDto(RequiresOtp: true, UserId: userId);
    }
}
