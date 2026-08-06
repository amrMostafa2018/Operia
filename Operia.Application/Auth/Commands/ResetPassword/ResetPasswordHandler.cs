using MediatR;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Common.PhoneNumbers;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Auth.Commands.ResetPassword;

public sealed class ResetPasswordHandler : IRequestHandler<ResetPasswordCommand>
{
    private readonly IIdentityService _identityService;

    public ResetPasswordHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var phoneNumber = PhoneNumberHelper.ToE164(request.PhoneNumber);
        var userId = await _identityService.GetUserIdByPhoneAsync(phoneNumber, cancellationToken);

        if (userId is null)
            throw UnauthorizedException.FromCode(ApiErrorCodes.Auth.OtpInvalid, "detail");

        await _identityService.ResetPasswordAsync(
            userId,
            request.ResetToken,
            request.NewPassword,
            cancellationToken);
    }
}
