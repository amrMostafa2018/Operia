namespace Operia.Application.Settings;

public sealed record IdentitySettingsDto(string ActivityName, string? ContactPhone, string? WhatsappPhone,
    string? Email, string? MainAddress, string? About, string? PrimaryPhotoUrl, IReadOnlyList<string> AdditionalPhotoUrls);
public sealed record SettingsImageUpload(Stream Content, string FileName, string ContentType, bool IsPrimary);
public sealed record UpdateIdentitySettingsRequest(string ActivityName, string? ContactPhone, string? WhatsappPhone,
    string? Email, string? MainAddress, string? About, IReadOnlyList<string> ExistingPhotoUrls, IReadOnlyList<SettingsImageUpload> NewPhotos);

public sealed record PaymentMethodsDto(bool CashEnabled, bool BankTransferEnabled, string? Bank, string? BankAccountHolder,
    string? BankAccountNumber, string? Iban, bool InstapayEnabled, string? InstapayId, string? InstapayAccountHolder,
    bool EWalletEnabled, string? WalletType, string? WalletHolderName, string? WalletNumber, bool FawryEnabled,
    string? FawryServiceCode, string? FawryNotes)
{
    public static PaymentMethodsDto Default => new(true, false, null, null, null, null, false, null, null, false, null, null, null, false, null, null);
}
public sealed record WorkingDayDto(string Day, bool Enabled, TimeOnly FromTime, TimeOnly ToTime)
{
    public static IReadOnlyList<WorkingDayDto> DefaultWeek =>
    [new("fri", false, new(9, 0), new(21, 0)), new("sat", true, new(9, 0), new(21, 0)), new("sun", true, new(9, 0), new(21, 0)), new("mon", true, new(9, 0), new(21, 0)), new("tue", true, new(9, 0), new(21, 0)), new("wed", true, new(9, 0), new(21, 0)), new("thu", true, new(9, 0), new(21, 0))];
}
public sealed record WorkingDaysSettingsDto(IReadOnlyList<WorkingDayDto> Days, bool AllowBookingOutsideWorkingHours);
public sealed record AuthorizedUserDto(string Id, string Name, string Email, bool IsBanned);
public sealed record SecuritySettingsDto(bool EnableTwoFactorAuthentication, bool LoginAlertsEnabled, string MaskedPhone, IReadOnlyList<AuthorizedUserDto> Users);
public sealed record UpdateSecurityRequest(bool EnableTwoFactorAuthentication, bool LoginAlertsEnabled, bool LogoutOtherDevices);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string OtpCode);
