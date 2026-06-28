using Operia.SharedKernel.Errors;using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Exceptions;
using Operia.Infrastructure.Options;
using Operia.SharedKernel.Interfaces;
namespace Operia.Infrastructure.Identity;

public sealed class OtpService : IOtpService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOtpSender _otpSender;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly OtpSettings _otpSettings;

    public OtpService(
        UserManager<ApplicationUser> userManager,
        IOtpSender otpSender,
        IDateTimeProvider dateTimeProvider,
        IOptions<OtpSettings> otpSettings)
    {
        _userManager = userManager;
        _otpSender = otpSender;
        _dateTimeProvider = dateTimeProvider;
        _otpSettings = otpSettings.Value;
    }
    public async Task GenerateAndSendOtpAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(nameof(ApplicationUser), userId);

        var otp = OtpGenerator.Generate(
            _userManager.PasswordHasher,
            user,
            _dateTimeProvider,
            _otpSettings.ExpiryMinutes);

        user.OtpHash = otp.Hash;
        user.OtpExpiry = otp.Expiry;

        await _userManager.UpdateAsync(user);

        if (string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            throw new ValidationException([
                ValidationFailureFactory.Create("phoneNumber", ApiErrorCodes.Auth.OtpPhoneRequired)
            ]);
        }

        await _otpSender.SendOtpAsync(user.PhoneNumber, otp.Code, cancellationToken);
    }

    public async Task VerifyOtpAsync(string userId, string code, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is null || user.OtpHash is null || user.OtpExpiry is null)
            throw UnauthorizedException.FromCode(ApiErrorCodes.Auth.OtpInvalid, "code");

        if (user.OtpExpiry < _dateTimeProvider.UtcNow)
            throw UnauthorizedException.FromCode(ApiErrorCodes.Auth.OtpExpired, "code");

        var result = _userManager.PasswordHasher.VerifyHashedPassword(user, user.OtpHash, code);

        if (result == PasswordVerificationResult.Failed)
            throw UnauthorizedException.FromCode(ApiErrorCodes.Auth.OtpInvalid, "code");

        user.OtpHash = null;
        user.OtpExpiry = null;
        await _userManager.UpdateAsync(user);
    }
}
