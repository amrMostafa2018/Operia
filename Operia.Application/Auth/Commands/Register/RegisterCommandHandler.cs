using MediatR;
using Operia.Application.Auth.DTOs;
using Operia.Application.Common.Interfaces;

namespace Operia.Application.Auth.Commands.Register;

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, RegisterResultDto>
{
    private readonly IRegistrationService _registrationService;

    public RegisterCommandHandler(IRegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    public Task<RegisterResultDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        return _registrationService.InitiateRegistrationAsync(
            request.Email,
            request.Password,
            request.PhoneNumber,
            cancellationToken);
    }
}
