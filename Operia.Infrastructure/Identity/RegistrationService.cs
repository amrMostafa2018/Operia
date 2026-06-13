using System.Security.Cryptography;
using FluentValidation.Results;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Operia.Application.Auth.DTOs;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Entities;
using Operia.Domain.Exceptions;
using Operia.Domain.Interfaces;
using Operia.Infrastructure.Options;
using Operia.SharedKernel.Interfaces;

namespace Operia.Infrastructure.Identity;

public sealed class RegistrationService : IRegistrationService
{
    private const string PasswordProtectorPurpose = "Registration.Password";

    private readonly IRegistrationRequestRepository _registrationRequestRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOtpSender _otpSender;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly OtpSettings _otpSettings;
    private readonly IDataProtector _passwordProtector;
    private readonly IUnitOfWork _unitOfWork;

    public RegistrationService(
        IRegistrationRequestRepository registrationRequestRepository,
        UserManager<ApplicationUser> userManager,
        IOtpSender otpSender,
        IDateTimeProvider dateTimeProvider,
        IOptions<OtpSettings> otpSettings,
        IDataProtectionProvider dataProtectionProvider,
        IUnitOfWork unitOfWork)
    {
        _registrationRequestRepository = registrationRequestRepository;
        _userManager = userManager;
        _otpSender = otpSender;
        _dateTimeProvider = dateTimeProvider;
        _otpSettings = otpSettings.Value;
        _passwordProtector = dataProtectionProvider.CreateProtector(PasswordProtectorPurpose);
        _unitOfWork = unitOfWork;
    }

    public async Task<RegisterResultDto> InitiateRegistrationAsync(
        string email,
        string password,
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            throw new ValidationException([
                new ValidationFailure("email", "Email is already registered.")
            ]);
        }

        await ValidatePasswordAsync(email, password);

        //TODO: Email Or Phone Number
        var existingRequests = await _registrationRequestRepository.GetByEmailAsync(email, cancellationToken);
        _registrationRequestRepository.RemoveRange(existingRequests);

        var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var otpHasher = new ApplicationUser();

        var request = new RegistrationRequest
        {
            Email = email,
            ProtectedPassword = _passwordProtector.Protect(password),
            PhoneNumber = phoneNumber,
            OtpHash = _userManager.PasswordHasher.HashPassword(otpHasher, code),
            OtpExpiry = _dateTimeProvider.UtcNow.AddMinutes(_otpSettings.ExpiryMinutes),
            CreatedAt = _dateTimeProvider.UtcNow
        };

        await _registrationRequestRepository.AddAsync(request, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _otpSender.SendOtpAsync(phoneNumber, code, cancellationToken);

        return new RegisterResultDto(
            RequiresOtp: true,
            RegistrationId: request.Id);
    }

    public async Task<string> CompleteRegistrationAsync(
        string registrationId,
        string code,
        CancellationToken cancellationToken = default)
    {
        var request = await _registrationRequestRepository.GetByIdAsync(registrationId, cancellationToken)
            ?? throw new NotFoundException(nameof(RegistrationRequest), registrationId);

        if (request.OtpExpiry < _dateTimeProvider.UtcNow)
            throw new UnauthorizedException("OTP has expired.");

        var otpHasher = new ApplicationUser();
        var otpResult = _userManager.PasswordHasher.VerifyHashedPassword(otpHasher, request.OtpHash, code);

        if (otpResult == PasswordVerificationResult.Failed)
            throw new UnauthorizedException("Invalid OTP code.");

        if (await _userManager.FindByEmailAsync(request.Email) is not null)
        {
            throw new ValidationException([
                new ValidationFailure("email", "Email is already registered.")
            ]);
        }

        var password = _passwordProtector.Unprotect(request.ProtectedPassword);

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            EmailConfirmed = true,
            PhoneNumberConfirmed = true
        };

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var createResult = await _userManager.CreateAsync(user, password);

            if (!createResult.Succeeded)
            {
                throw new ValidationException(
                    createResult.Errors.Select(e => new ValidationFailure("identity", e.Description)));
            }

            var roleResult = await _userManager.AddToRoleAsync(user, Roles.Admin);

            if (!roleResult.Succeeded)
            {
                throw new ValidationException(
                    roleResult.Errors.Select(e => new ValidationFailure("identity", e.Description)));
            }

            _registrationRequestRepository.Remove(request);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return user.Id;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private async Task ValidatePasswordAsync(string email, string password)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email
        };

        var errors = new List<IdentityError>();

        foreach (var validator in _userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(_userManager, user, password);

            if (!result.Succeeded)
                errors.AddRange(result.Errors);
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(
                errors.Select(e => new ValidationFailure("password", e.Description)));
        }
    }
}
