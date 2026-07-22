using Microsoft.AspNetCore.Identity;
using Operia.Domain.Entities;

namespace Operia.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public string? OtpHash { get; set; }
    public DateTime? OtpExpiry { get; set; }
    public string? ResetPasswordOtpHash { get; set; }
    public DateTime? ResetPasswordOtpExpiry { get; set; }
    public bool MustChangePassword { get; set; }
    public bool LoginAlertsEnabled { get; set; } = true;
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
