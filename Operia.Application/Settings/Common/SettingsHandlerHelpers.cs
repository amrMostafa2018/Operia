using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Entities;

namespace Operia.Application.Settings.Common;

internal static class SettingsHandlerHelpers
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string RequireTenant(ICurrentUserService currentUserService)
        => currentUserService.TenantId ?? throw new UnauthorizedAccessException("The current user does not have a tenant.");

    public static string RequireUser(ICurrentUserService currentUserService)
        => currentUserService.UserId ?? throw new UnauthorizedAccessException("The current user is not authenticated.");

    public static async Task<Business> GetBusinessAsync(IApplicationDbContext db, string tenantId, CancellationToken cancellationToken)
        => await db.Businesses.FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
           ?? throw new InvalidOperationException("The current tenant has no business profile.");

    public static async Task<BusinessSettings> GetBusinessSettingsAsync(IApplicationDbContext db, string tenantId, CancellationToken cancellationToken)
    {
        var business = await GetBusinessAsync(db, tenantId, cancellationToken);
        var settings = await db.BusinessSettings.FirstOrDefaultAsync(x => x.BusinessId == business.Id, cancellationToken);
        if (settings is not null)
        {
            return settings;
        }

        settings = new BusinessSettings { BusinessId = business.Id };
        await db.BusinessSettings.AddAsync(settings, cancellationToken);
        return settings;
    }

    public static IdentitySettingsDto ToIdentity(
        Business business,
        IEnumerable<BusinessGallery> photos,
        UserSummaryDto? registrationFallback = null)
        => new(
            business.ActivityName,
            FirstNonEmpty(business.MobileNumber, registrationFallback?.PhoneNumber),
            FirstNonEmpty(business.WhatsappNumber, registrationFallback?.PhoneNumber),
            FirstNonEmpty(business.Email, registrationFallback?.Email),
            business.MainBranchAddress,
            business.Description,
            photos.FirstOrDefault(x => x.IsMainImage)?.ImageUrl,
            photos.Where(x => !x.IsMainImage).Select(x => x.ImageUrl).ToArray());

    private static string? FirstNonEmpty(string? value, string? fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

    public static T Deserialize<T>(string? json, T fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
            return fallback;
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    public static string Mask(string? phone)
        => string.IsNullOrWhiteSpace(phone) || phone.Length < 4 ? string.Empty : $"*** *** {phone[^4..]}";
}
