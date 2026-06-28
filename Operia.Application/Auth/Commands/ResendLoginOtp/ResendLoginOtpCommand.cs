using MediatR;

namespace Operia.Application.Auth.Commands.ResendLoginOtp;

public sealed record ResendLoginOtpCommand(string UserId) : IRequest;
