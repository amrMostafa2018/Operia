using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Interfaces;
using Operia.Application.Settings;
using Operia.Domain.Entities;
using Operia.Infrastructure.Identity;
using Operia.Infrastructure.Persistence;

namespace Operia.Infrastructure.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _files;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IOtpService _otp;
    public SettingsService(ApplicationDbContext db, IFileStorageService files, UserManager<ApplicationUser> users, IOtpService otp)
        => (_db, _files, _users, _otp) = (db, files, users, otp);

    public async Task<IdentitySettingsDto> GetIdentityAsync(string tenantId, CancellationToken ct = default)
    {
        var business = await GetBusinessAsync(tenantId, ct);
        var photos = await _db.BusinessGalleries.AsNoTracking().Where(x => x.BusinessId == business.Id).OrderByDescending(x => x.IsMainImage).ThenBy(x => x.UploadedAt).ToListAsync(ct);
        return ToIdentity(business, photos);
    }
    public async Task<IdentitySettingsDto> UpdateIdentityAsync(string tenantId, UpdateIdentitySettingsRequest request, CancellationToken ct = default)
    {
        var business = await GetBusinessAsync(tenantId, ct);
        business.ActivityName = request.ActivityName.Trim(); business.MobileNumber = request.ContactPhone?.Trim(); business.WhatsappNumber = request.WhatsappPhone?.Trim(); business.Email = request.Email?.Trim(); business.MainBranchAddress = request.MainAddress?.Trim(); business.Description = request.About?.Trim();
        var photos = await _db.BusinessGalleries.Where(x => x.BusinessId == business.Id).OrderBy(x => x.UploadedAt).ToListAsync(ct);
        var kept = request.ExistingPhotoUrls.Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.Ordinal);
        if (kept.Count + request.NewPhotos.Count > 6) throw new InvalidOperationException("A maximum of six business photos is allowed.");
        foreach (var item in photos.Where(x => !kept.Contains(x.ImageUrl)).ToList()) { await _files.DeleteAsync(item.ImageUrl, ct); _db.BusinessGalleries.Remove(item); photos.Remove(item); }
        foreach (var upload in request.NewPhotos)
        {
            if (upload.IsPrimary) foreach (var previous in photos.Where(x => x.IsMainImage)) previous.IsMainImage = false;
            var url = await _files.SaveAsync(upload.Content, upload.FileName, upload.ContentType, business.TenantId, FileUploadCategory.BusinessGallery, ct);
            var gallery = new BusinessGallery { BusinessId = business.Id, ImageUrl = url, IsMainImage = upload.IsPrimary, UploadedAt = DateTime.UtcNow };
            await _db.BusinessGalleries.AddAsync(gallery, ct); photos.Add(gallery);
        }
        if (photos.Count > 0 && photos.All(x => !x.IsMainImage)) photos[0].IsMainImage = true;
        await _db.SaveChangesAsync(ct); return ToIdentity(business, photos.OrderByDescending(x => x.IsMainImage).ThenBy(x => x.UploadedAt));
    }
    public async Task<PaymentMethodsDto> GetPaymentMethodsAsync(string tenantId, CancellationToken ct = default) => Deserialize((await GetBusinessSettingsAsync(tenantId, ct)).PaymentMethodsJson, PaymentMethodsDto.Default);
    public async Task<PaymentMethodsDto> UpdatePaymentMethodsAsync(string tenantId, PaymentMethodsDto request, CancellationToken ct = default) { var s = await GetBusinessSettingsAsync(tenantId, ct); s.PaymentMethodsJson = JsonSerializer.Serialize(request, JsonOptions); await _db.SaveChangesAsync(ct); return request; }
    public async Task<WorkingDaysSettingsDto> GetWorkingDaysAsync(string tenantId, CancellationToken ct = default)
    {
        var s = await GetBusinessSettingsAsync(tenantId, ct);
        var days = Deserialize(s.WorkingDaysJson, WorkingDayDto.DefaultWeek);
        return new(days.Count == 0 ? WorkingDayDto.DefaultWeek : days, s.AllowBookingOutsideWorkingHours);
    }
    public async Task<WorkingDaysSettingsDto> UpdateWorkingDaysAsync(string tenantId, WorkingDaysSettingsDto request, CancellationToken ct = default) { var s = await GetBusinessSettingsAsync(tenantId, ct); s.WorkingDaysJson = JsonSerializer.Serialize(request.Days, JsonOptions); s.AllowBookingOutsideWorkingHours = request.AllowBookingOutsideWorkingHours; await _db.SaveChangesAsync(ct); return request; }
    public async Task<SecuritySettingsDto> GetSecurityAsync(string userId, CancellationToken ct = default) { var current = await GetUserAsync(userId); var list = await _users.Users.AsNoTracking().Where(x => x.TenantId == current.TenantId).OrderBy(x => x.UserName).Select(x => new AuthorizedUserDto(x.Id, x.UserName ?? x.Email ?? x.PhoneNumber ?? "User", x.Email ?? string.Empty, x.LockoutEnd.HasValue && x.LockoutEnd > DateTimeOffset.UtcNow)).ToListAsync(ct); return new(current.TwoFactorEnabled, current.LoginAlertsEnabled, Mask(current.PhoneNumber), list); }
    public async Task UpdateSecurityAsync(string userId, UpdateSecurityRequest request, CancellationToken ct = default) { var u = await GetUserAsync(userId); u.TwoFactorEnabled = request.EnableTwoFactorAuthentication; u.LoginAlertsEnabled = request.LoginAlertsEnabled; await EnsureAsync(await _users.UpdateAsync(u)); if (request.LogoutOtherDevices) await _users.UpdateSecurityStampAsync(u); }
    public Task SendPasswordOtpAsync(string userId, CancellationToken ct = default) => _otp.GenerateAndSendOtpAsync(userId, ct);
    public async Task ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken ct = default) { await _otp.VerifyOtpAsync(userId, request.OtpCode, ct); await EnsureAsync(await _users.ChangePasswordAsync(await GetUserAsync(userId), request.CurrentPassword, request.NewPassword)); }
    public async Task BanUserAsync(string userId, string targetUserId, CancellationToken ct = default) { var u = await FindTenantUserAsync(await GetUserAsync(userId), targetUserId); await _users.SetLockoutEnabledAsync(u, true); await _users.SetLockoutEndDateAsync(u, DateTimeOffset.UtcNow.AddYears(100)); await _users.UpdateSecurityStampAsync(u); }
    public async Task DeleteUserAsync(string userId, string targetUserId, CancellationToken ct = default) => await EnsureAsync(await _users.DeleteAsync(await FindTenantUserAsync(await GetUserAsync(userId), targetUserId)));
    public async Task DeactivateAccountAsync(string userId, CancellationToken ct = default) { var u = await GetUserAsync(userId); await _users.SetLockoutEnabledAsync(u, true); await _users.SetLockoutEndDateAsync(u, DateTimeOffset.UtcNow.AddYears(100)); await _users.UpdateSecurityStampAsync(u); }
    private async Task<Business> GetBusinessAsync(string tenantId, CancellationToken ct) => await _db.Businesses.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct) ?? throw new InvalidOperationException("The current tenant has no business profile.");
    private async Task<BusinessSettings> GetBusinessSettingsAsync(string tenantId, CancellationToken ct) { var b = await GetBusinessAsync(tenantId, ct); var s = await _db.BusinessSettings.FirstOrDefaultAsync(x => x.BusinessId == b.Id, ct); if (s is not null) return s; s = new() { BusinessId = b.Id }; await _db.BusinessSettings.AddAsync(s, ct); return s; }
    private async Task<ApplicationUser> GetUserAsync(string id) => await _users.FindByIdAsync(id) ?? throw new UnauthorizedAccessException();
    private async Task<ApplicationUser> FindTenantUserAsync(ApplicationUser current, string targetId) { if (current.Id == targetId) throw new InvalidOperationException("You cannot modify your own account."); return await _users.Users.FirstOrDefaultAsync(x => x.Id == targetId && x.TenantId == current.TenantId) ?? throw new KeyNotFoundException("User not found in the current tenant."); }
    private static IdentitySettingsDto ToIdentity(Business b, IEnumerable<BusinessGallery> p) => new(b.ActivityName, b.MobileNumber, b.WhatsappNumber, b.Email, b.MainBranchAddress, b.Description, p.FirstOrDefault(x => x.IsMainImage)?.ImageUrl, p.Where(x => !x.IsMainImage).Select(x => x.ImageUrl).ToArray());
    private static T Deserialize<T>(string json, T fallback) { try { return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? fallback; } catch (JsonException) { return fallback; } }
    private static string Mask(string? phone) => string.IsNullOrWhiteSpace(phone) || phone.Length < 4 ? string.Empty : $"*** *** {phone[^4..]}";
    private static async Task EnsureAsync(IdentityResult result) { if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description))); await Task.CompletedTask; }
}
