using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.Auth.Commands.ForgotPassword;
using Operia.Application.Auth.Commands.Login;
using Operia.Application.Auth.Commands.Logout;
using Operia.Application.Auth.Commands.RefreshToken;
using Operia.Application.Auth.Commands.Register;
using Operia.Application.Auth.Commands.ResendForgotPasswordOtp;
using Operia.Application.Auth.Commands.ResendLoginOtp;
using Operia.Application.Auth.Commands.ResendRegisterOtp;
using Operia.Application.Auth.Commands.ResetPassword;
using Operia.Application.Auth.Commands.VerifyForgotPasswordOtp;
using Operia.Application.Auth.Commands.VerifyOtp;
using Operia.Application.Auth.Commands.VerifyRegisterOtp;
using Operia.Application.Auth.Commands.CompleteFirstLogin;
using Operia.Application.Auth.DTOs;

namespace Operia.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RegisterResultDto>> Register(
        [FromBody] RegisterCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    [HttpPost("verify-register-otp")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponseDto>> VerifyRegisterOtp(
        [FromBody] VerifyRegisterOtpCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    [HttpPost("resend-register-otp")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResendRegisterOtp(
        [FromBody] ResendRegisterOtpCommand command,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LoginResultDto>> Login([FromBody] LoginCommand command, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    [HttpPost("verify-otp")]
    [ProducesResponseType(typeof(VerifyLoginOtpResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VerifyLoginOtpResultDto>> VerifyOtp([FromBody] VerifyOtpCommand command, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    [HttpPost("complete-first-login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponseDto>> CompleteFirstLogin([FromBody] CompleteFirstLoginCommand command, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(command, cancellationToken));

    [HttpPost("resend-login-otp")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResendLoginOtp(
        [FromBody] ResendLoginOtpCommand command,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponseDto>> Refresh([FromBody] RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await _mediator.Send(new LogoutCommand(), cancellationToken);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordCommand command,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpPost("verify-forgot-password-otp")]
    [ProducesResponseType(typeof(VerifyForgotPasswordOtpResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VerifyForgotPasswordOtpResultDto>> VerifyForgotPasswordOtp(
        [FromBody] VerifyForgotPasswordOtpCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordCommand command,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpPost("resend-forgot-password-otp")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResendForgotPasswordOtp(
        [FromBody] ResendForgotPasswordOtpCommand command,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }
}
