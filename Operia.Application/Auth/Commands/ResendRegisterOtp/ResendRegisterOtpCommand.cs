using MediatR;

namespace Operia.Application.Auth.Commands.ResendRegisterOtp;

public sealed record ResendRegisterOtpCommand(string RegistrationId) : IRequest;
