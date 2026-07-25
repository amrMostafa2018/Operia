using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.Auth;
using Operia.Application.Settings;
using Operia.Application.Settings.Commands.BanSettingsUser;
using Operia.Application.Settings.Commands.ChangePassword;
using Operia.Application.Settings.Commands.DeactivateAccount;
using Operia.Application.Settings.Commands.DeleteSettingsUser;
using Operia.Application.Settings.Commands.SendPasswordOtp;
using Operia.Application.Settings.Commands.UpdatePaymentMethods;
using Operia.Application.Settings.Commands.UpdateSecuritySettings;
using Operia.Application.Settings.Commands.UpdateWorkingDays;
using Operia.Application.Settings.Queries.GetPaymentMethods;
using Operia.Application.Settings.Queries.GetSecuritySettings;
using Operia.Application.Settings.Queries.GetWorkingDays;

namespace Operia.Controllers;

[ApiController]
[Authorize(Policy = Policies.SettingsManage)]
[Route("api/settings")]
public sealed class SettingsManagementController(IMediator mediator) : ControllerBase
{
    [HttpGet("payment-methods")] public async Task<ActionResult<PaymentMethodsDto>> GetPaymentMethods(CancellationToken ct) => Ok(await mediator.Send(new GetPaymentMethodsQuery(), ct));
    [HttpPut("payment-methods")] public async Task<ActionResult<PaymentMethodsDto>> UpdatePaymentMethods([FromBody] PaymentMethodsDto request, CancellationToken ct) => Ok(await mediator.Send(new UpdatePaymentMethodsCommand(request), ct));
    [HttpGet("working-days")] public async Task<ActionResult<WorkingDaysSettingsDto>> GetWorkingDays(CancellationToken ct) => Ok(await mediator.Send(new GetWorkingDaysQuery(), ct));
    [HttpPut("working-days")] public async Task<ActionResult<WorkingDaysSettingsDto>> UpdateWorkingDays([FromBody] WorkingDaysSettingsDto request, CancellationToken ct) => Ok(await mediator.Send(new UpdateWorkingDaysCommand(request), ct));
    [HttpGet("security")] public async Task<ActionResult<SecuritySettingsDto>> GetSecurity(CancellationToken ct) => Ok(await mediator.Send(new GetSecuritySettingsQuery(), ct));
    [HttpPut("security")] public async Task<IActionResult> UpdateSecurity([FromBody] UpdateSecurityRequest request, CancellationToken ct) { await mediator.Send(new UpdateSecuritySettingsCommand(request), ct); return NoContent(); }
    [HttpPost("security/password-otp")] public async Task<IActionResult> SendPasswordOtp(CancellationToken ct) { await mediator.Send(new SendPasswordOtpCommand(), ct); return NoContent(); }
    [HttpPost("security/change-password")] public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct) { await mediator.Send(new ChangePasswordCommand(request), ct); return NoContent(); }
    [HttpPost("security/users/{userId}/ban")] public async Task<IActionResult> BanUser(string userId, CancellationToken ct) { await mediator.Send(new BanSettingsUserCommand(userId), ct); return NoContent(); }
    [HttpDelete("security/users/{userId}")] public async Task<IActionResult> DeleteUser(string userId, CancellationToken ct) { await mediator.Send(new DeleteSettingsUserCommand(userId), ct); return NoContent(); }
    [HttpPost("security/deactivate-account")] public async Task<IActionResult> Deactivate(CancellationToken ct) { await mediator.Send(new DeactivateAccountCommand(), ct); return NoContent(); }
}
