namespace Operia.Application.Packages;

/// <summary>Computes the entitlement after a confirmed collection without consuming sessions.</summary>
public static class InstallmentSessionRelease
{
    public static int AvailableAfterPayment(
        int totalSessions,
        int previouslyReleased,
        int cumulativePlannedRelease,
        bool paymentConfirmed,
        bool fullyPaid)
    {
        if (!paymentConfirmed)
        {
            return previouslyReleased;
        }

        var target = fullyPaid
            ? totalSessions
            : Math.Min(cumulativePlannedRelease, InstallmentPlanRules.PrepaymentReleaseLimit(totalSessions));
        return Math.Max(previouslyReleased, target);
    }
}
