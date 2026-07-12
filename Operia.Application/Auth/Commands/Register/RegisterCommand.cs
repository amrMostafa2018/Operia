using MediatR;
using Operia.Application.Auth.DTOs;

namespace Operia.Application.Auth.Commands.Register;

public sealed record RegisterCommand(
    string FullName,
    string Email,
    string Password,
    string ConfirmPassword,
    string PhoneNumber) : IRequest<RegisterResultDto>;
