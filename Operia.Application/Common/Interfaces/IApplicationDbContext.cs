using Microsoft.EntityFrameworkCore;
using Operia.Domain.Entities;

namespace Operia.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }
    DbSet<TenantSubscription> TenantSubscriptions { get; }
    DbSet<Business> Businesses { get; }
    DbSet<BusinessGallery> BusinessGalleries { get; }
    DbSet<PlatformRevenue> PlatformRevenues { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
