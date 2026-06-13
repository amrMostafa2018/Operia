using System.Security.Cryptography;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
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

        var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        user.OtpHash = _userManager.PasswordHasher.HashPassword(user, code);
        user.OtpExpiry = _dateTimeProvider.UtcNow.AddMinutes(_otpSettings.ExpiryMinutes);

        await _userManager.UpdateAsync(user);

        if (string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            throw new ValidationException([
                new ValidationFailure("phoneNumber", "User phone number is required for OTP delivery.")
            ]);
        }

        await _otpSender.SendOtpAsync(user.PhoneNumber, code, cancellationToken);
    }

    public async Task<bool> VerifyOtpAsync(string userId, string code, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is null || user.OtpHash is null || user.OtpExpiry is null)
            return false;

        if (user.OtpExpiry < _dateTimeProvider.UtcNow)
            return false;

        var result = _userManager.PasswordHasher.VerifyHashedPassword(user, user.OtpHash, code);

        if (result == PasswordVerificationResult.Failed)
            return false;

        user.OtpHash = null;
        user.OtpExpiry = null;
        await _userManager.UpdateAsync(user);

        return true;
    }
}
