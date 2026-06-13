using MediatR;
using Operia.Application.Auth.DTOs;

namespace Operia.Application.Auth.Commands.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<LoginResultDto>;
