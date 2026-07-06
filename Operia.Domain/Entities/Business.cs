using Operia.Domain.Common;
using Operia.Domain.Enums;

namespace Operia.Domain.Entities;

public sealed class Business : Entity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string ActivityName { get; set; } = string.Empty;
    public string? MobileNumber { get; set; }
    public string? WhatsappNumber { get; set; }
    public string? Email { get; set; }
    public string? MainBranchAddress { get; set; }
    public string? Description { get; set; }
    public BusinessStatus Status { get; set; } = BusinessStatus.Active;

    public Tenant? Tenant { get; set; }
    public ICollection<BusinessGallery> Gallery { get; set; } = [];
}
