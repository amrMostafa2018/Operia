using MediatR;
using Operia.Application.Auth.DTOs;

namespace Operia.Application.Auth.Commands.VerifyOtp;

public sealed record VerifyOtpCommand(string UserId, string Code) : IRequest<AuthResponseDto>;
