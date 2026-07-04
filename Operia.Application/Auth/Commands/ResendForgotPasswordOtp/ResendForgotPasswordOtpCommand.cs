using MediatR;

namespace Operia.Application.Auth.Commands.ResendForgotPasswordOtp;

public sealed record ResendForgotPasswordOtpCommand(string PhoneNumber) : IRequest;
