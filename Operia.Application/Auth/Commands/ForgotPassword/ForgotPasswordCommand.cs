using MediatR;

namespace Operia.Application.Auth.Commands.ForgotPassword;

public sealed record ForgotPasswordCommand(string PhoneNumber) : IRequest;
