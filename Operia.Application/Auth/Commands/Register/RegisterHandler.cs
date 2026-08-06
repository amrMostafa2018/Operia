using MediatR;
using Operia.Application.Auth.DTOs;
using Operia.Application.Common.Interfaces;
using Operia.Application.Common.PhoneNumbers;

namespace Operia.Application.Auth.Commands.Register;

public sealed class RegisterHandler : IRequestHandler<RegisterCommand, RegisterResultDto>
{
    private readonly IRegistrationService _registrationService;

    public RegisterHandler(IRegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    public Task<RegisterResultDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var phoneNumber = PhoneNumberHelper.ToE164(request.PhoneNumber);

        return _registrationService.InitiateRegistrationAsync(
            request.FullName,
            request.Email,
            request.Password,
            phoneNumber,
            cancellationToken);
    }
}
