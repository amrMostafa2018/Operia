using MediatR;
using Operia.Application.Auth.DTOs;

namespace Operia.Application.Auth.Commands.VerifyForgotPasswordOtp;

public sealed record VerifyForgotPasswordOtpCommand(string PhoneNumber, string OtpCode)
    : IRequest<VerifyForgotPasswordOtpResultDto>;
