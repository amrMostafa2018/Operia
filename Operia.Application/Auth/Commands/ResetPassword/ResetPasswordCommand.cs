using MediatR;

namespace Operia.Application.Auth.Commands.ResetPassword;

public sealed record ResetPasswordCommand(
    string PhoneNumber,
    string ResetToken,
    string NewPassword,
    string ConfirmPassword) : IRequest;
