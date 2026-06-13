using Microsoft.AspNetCore.Identity;
using Operia.Domain.Entities;

namespace Operia.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public string? OtpHash { get; set; }
    public DateTime? OtpExpiry { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
