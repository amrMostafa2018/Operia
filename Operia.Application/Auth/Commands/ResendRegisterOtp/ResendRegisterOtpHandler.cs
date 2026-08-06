using MediatR;
using Operia.Application.Common.Interfaces;

namespace Operia.Application.Auth.Commands.ResendRegisterOtp;

public sealed class ResendRegisterOtpHandler : IRequestHandler<ResendRegisterOtpCommand>
{
    private readonly IRegistrationService _registrationService;

    public ResendRegisterOtpHandler(IRegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    public Task Handle(ResendRegisterOtpCommand request, CancellationToken cancellationToken) =>
        _registrationService.ResendRegistrationOtpAsync(request.RegistrationId, cancellationToken);
}
