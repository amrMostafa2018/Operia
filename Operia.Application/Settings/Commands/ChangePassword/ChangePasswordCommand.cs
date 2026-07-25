using MediatR;

namespace Operia.Application.Settings.Commands.ChangePassword;

public sealed record ChangePasswordCommand(ChangePasswordRequest Request) : IRequest;
