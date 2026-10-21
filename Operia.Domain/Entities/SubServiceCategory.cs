using Operia.Domain.Common;
using Operia.Domain.Interfaces;

namespace Operia.Domain.Entities;

public sealed class SubServiceCategory : Entity, ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string BusinessId { get; set; } = string.Empty;
    public string ServiceCategoryId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Business? Business { get; set; }
    public ServiceCategory? ServiceCategory { get; set; }
    public ICollection<Package> Packages { get; set; } = [];
}
