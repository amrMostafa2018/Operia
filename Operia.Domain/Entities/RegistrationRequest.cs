namespace Operia.Domain.Entities;

public sealed class RegistrationRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Email { get; set; } = string.Empty;
    public string ProtectedPassword { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string OtpHash { get; set; } = string.Empty;
    public DateTime OtpExpiry { get; set; }
    public DateTime CreatedAt { get; set; }
}
