namespace Operia.Domain.Enums;

/// <summary>Distinguishes Package sessions, catalog services, and unlisted services in saved bookings.</summary>
public enum BookingItemType
{
    PackageSession = 1,
    Service = 2,
    UnlistedService = 3
}
