using Operia.SharedKernel.Errors;
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
    private readonly IIdentityService _identityService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOtpSender _otpSender;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly OtpSettings _otpSettings;
    private readonly IDataProtector _passwordProtector;
    private readonly IUnitOfWork _unitOfWork;

    public RegistrationService(
        IRegistrationRequestRepository registrationRequestRepository,
        IIdentityService identityService,
        UserManager<ApplicationUser> userManager,
        IOtpSender otpSender,
        IDateTimeProvider dateTimeProvider,
        IOptions<OtpSettings> otpSettings,
        IDataProtectionProvider dataProtectionProvider,
        IUnitOfWork unitOfWork)
    {
        _registrationRequestRepository = registrationRequestRepository;
        _identityService = identityService;
        _userManager = userManager;
        _otpSender = otpSender;
        _dateTimeProvider = dateTimeProvider;
        _otpSettings = otpSettings.Value;
        _passwordProtector = dataProtectionProvider.CreateProtector(PasswordProtectorPurpose);
        _unitOfWork = unitOfWork;
    }

    public async Task<RegisterResultDto> InitiateRegistrationAsync(
        string fullName,
        string email,
        string password,
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        await ValidatePasswordAsync(phoneNumber, password);

        var existingRequests = await _registrationRequestRepository.GetByPhoneAsync(phoneNumber, cancellationToken);
        _registrationRequestRepository.RemoveRange(existingRequests);

        var request = new RegistrationRequest
        {
            FullName = fullName.Trim(),
            Email = email,
            ProtectedPassword = _passwordProtector.Protect(password),
            PhoneNumber = phoneNumber,
            CreatedAt = _dateTimeProvider.UtcNow
        };

        var code = SetNewOtp(request);

        await _registrationRequestRepository.AddAsync(request, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _otpSender.SendOtpAsync(phoneNumber, code, cancellationToken);

        return new RegisterResultDto(
            RequiresOtp: true,
            RegistrationId: request.Id);
    }

    public async Task ResendRegistrationOtpAsync(
        string registrationId,
        CancellationToken cancellationToken = default)
    {
        var request = await _registrationRequestRepository.GetByIdAsync(registrationId, cancellationToken)
            ?? throw new NotFoundException(nameof(RegistrationRequest), registrationId);

        var code = SetNewOtp(request);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _otpSender.SendOtpAsync(request.PhoneNumber, code, cancellationToken);
    }

    public async Task<string> CompleteRegistrationAsync(
        string registrationId,
        string code,
        CancellationToken cancellationToken = default)
    {
        var request = await _registrationRequestRepository.GetByIdAsync(registrationId, cancellationToken)
            ?? throw new NotFoundException(nameof(RegistrationRequest), registrationId);

        if (request.OtpExpiry < _dateTimeProvider.UtcNow)
            throw UnauthorizedException.FromCode(ApiErrorCodes.Auth.OtpExpired, "code");

        var otpHasher = new ApplicationUser();
        var otpResult = _userManager.PasswordHasher.VerifyHashedPassword(otpHasher, request.OtpHash, code);

        if (otpResult == PasswordVerificationResult.Failed)
            throw UnauthorizedException.FromCode(ApiErrorCodes.Auth.OtpInvalid, "code");

        await EnsurePhoneAvailableAsync(request.PhoneNumber, cancellationToken);
        await EnsureEmailAvailableAsync(request.Email, cancellationToken);

        var password = _passwordProtector.Unprotect(request.ProtectedPassword);

        var user = new ApplicationUser
        {
            FullName = request.FullName,
            UserName = request.PhoneNumber,
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
                    createResult.Errors.Select(e =>
                        ValidationFailureFactory.Create("identity", ApiErrorCodes.Auth.IdentityError, e.Description)));
            }

            var roleResult = await _userManager.AddToRoleAsync(user, Roles.SuperAdmin);

            if (!roleResult.Succeeded)
            {
                throw new ValidationException(
                    roleResult.Errors.Select(e =>
                        ValidationFailureFactory.Create("identity", ApiErrorCodes.Auth.IdentityError, e.Description)));
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

    private string SetNewOtp(RegistrationRequest request)
    {
        var otp = OtpGenerator.Generate(
            _userManager.PasswordHasher,
            new ApplicationUser(),
            _dateTimeProvider,
            _otpSettings.ExpiryMinutes);

        request.OtpHash = otp.Hash;
        request.OtpExpiry = otp.Expiry;

        return otp.Code;
    }

    private async Task EnsurePhoneAvailableAsync(
        string phoneNumber,
        CancellationToken cancellationToken)
    {
        if (await _identityService.IsPhoneRegisteredAsync(phoneNumber, cancellationToken))
        {
            throw new ValidationException([
                ValidationFailureFactory.Create("phoneNumber", ApiErrorCodes.Auth.PhoneAlreadyRegistered)
            ]);
        }
    }

    private async Task EnsureEmailAvailableAsync(string email, CancellationToken cancellationToken)
    {
        if (await _identityService.IsEmailRegisteredAsync(email, cancellationToken))
        {
            throw new ValidationException([
                ValidationFailureFactory.Create("email", ApiErrorCodes.Auth.EmailAlreadyRegistered)
            ]);
        }
    }

    private async Task ValidatePasswordAsync(string phoneNumber, string password)
    {
        var user = new ApplicationUser
        {
            UserName = phoneNumber,
            PhoneNumber = phoneNumber
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
                errors.Select(e =>
                    ValidationFailureFactory.Create("password", ApiErrorCodes.Auth.PasswordMinLength, e.Description)));
        }
    }
}
