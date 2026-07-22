using MediatR;
using Operia.Application.Auth.DTOs;

namespace Operia.Application.Auth.Commands.CompleteFirstLogin;

public sealed record CompleteFirstLoginCommand(string UserId, string ResetToken, string NewPassword) : IRequest<AuthResponseDto>;
