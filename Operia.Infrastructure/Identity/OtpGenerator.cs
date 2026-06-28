using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Operia.SharedKernel.Interfaces;

namespace Operia.Infrastructure.Identity;

internal static class OtpGenerator
{
    public sealed record OtpCredentials(string Code, string Hash, DateTime Expiry);

    public static OtpCredentials Generate(
        IPasswordHasher<ApplicationUser> passwordHasher,
        ApplicationUser hashSubject,
        IDateTimeProvider dateTimeProvider,
        int expiryMinutes)
    {
        var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var hash = passwordHasher.HashPassword(hashSubject, code);
        var expiry = dateTimeProvider.UtcNow.AddMinutes(expiryMinutes);

        return new OtpCredentials(code, hash, expiry);
    }
}
