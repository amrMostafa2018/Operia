using Operia.Domain.Enums;

namespace Operia.Application.Bookings.Common;

/// <summary>Calculates customer package balances for pulse, session, and single-session purchases.</summary>
public static class CustomerPackageBalance
{
    /// <summary>True when the package master defines a pulse balance on a package offer.</summary>
    public static bool UsesPulses(OfferType offerType, int? pulseCount) =>
        offerType == OfferType.Package && pulseCount is > 0;

    /// <summary>
    /// Remaining units available on the purchase.
    /// Pulse packages ignore reserved slots. Single-session and session packages use
    /// <c>Total - Used - ReservedSessions</c>.
    /// </summary>
    public static int Remaining(
        int total,
        int used,
        int? reservedSessions,
        OfferType offerType,
        int? pulseCount) =>
        UsesPulses(offerType, pulseCount)
            ? Math.Max(0, total - used)
            : Math.Max(0, total - used - (reservedSessions ?? 0));

    /// <summary>True when the purchase still has remaining balance using the offer-type rules.</summary>
    public static bool HasAvailable(
        int total,
        int used,
        int? reservedSessions,
        OfferType offerType,
        int? pulseCount) =>
        Remaining(total, used, reservedSessions, offerType, pulseCount) > 0;
}
