using MediatR;
using Operia.Application.Auth.DTOs;

namespace Operia.Application.Auth.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResponseDto>;
