using MediatR;
using Operia.Application.Auth.DTOs;

namespace Operia.Application.Auth.Commands.VerifyRegisterOtp;

public sealed record VerifyRegisterOtpCommand(
    string RegistrationId,
    string Code) : IRequest<AuthResponseDto>;
