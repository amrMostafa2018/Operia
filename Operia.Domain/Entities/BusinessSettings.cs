using Operia.Domain.Common;

namespace Operia.Domain.Entities;

public sealed class BusinessSettings : Entity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string BusinessId { get; set; } = string.Empty;
    public string PaymentMethodsJson { get; set; } = "{}";
    public string WorkingDaysJson { get; set; } = "[]";
    public bool AllowBookingOutsideWorkingHours { get; set; }

    public Business? Business { get; set; }
}
