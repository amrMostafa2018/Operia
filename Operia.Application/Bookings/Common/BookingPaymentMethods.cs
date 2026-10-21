using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Interfaces;
using Operia.Application.Settings;
using Operia.Application.Settings.Common;

namespace Operia.Application.Bookings.Common;

/// <summary>Reads the enabled payment method identifiers for the authenticated tenant.</summary>
internal static class BookingPaymentMethods
{
    /// <summary>Returns enabled payment identifiers without exposing configured account details.</summary>
    public static async Task<IReadOnlyList<string>> GetEnabledAsync(
        IApplicationDbContext db,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var json = await db.Businesses.AsNoTracking()
            .Where(business => business.TenantId == tenantId)
            .Select(business => business.Settings == null
                ? null
                : business.Settings.PaymentMethodsJson)
            .SingleOrDefaultAsync(cancellationToken);
        var settings = string.IsNullOrWhiteSpace(json) || json.Trim() == "{}"
            ? PaymentMethodsDto.Default
            : SettingsHandlerHelpers.Deserialize(json, PaymentMethodsDto.Default);

        var enabled = new List<string>();
        if (settings.CashEnabled)
        {
            enabled.Add("cash");
        }
        if (settings.BankTransferEnabled)
        {
            enabled.Add("bank_transfer");
        }
        if (settings.InstapayEnabled)
        {
            enabled.Add("instapay");
        }
        if (settings.EWalletEnabled)
        {
            enabled.Add("e_wallet");
        }
        if (settings.FawryEnabled)
        {
            enabled.Add("fawry");
        }
        return enabled;
    }
}
