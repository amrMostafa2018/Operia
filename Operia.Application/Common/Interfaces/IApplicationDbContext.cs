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
    DbSet<BusinessSettings> BusinessSettings { get; }
    DbSet<PlatformRevenue> PlatformRevenues { get; }
    DbSet<Branch> Branches { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Employee> Employees { get; }
    DbSet<EmployeeWorkingDay> EmployeeWorkingDays { get; }
    DbSet<UserBranch> UserBranches { get; }
    DbSet<TenantNumberCounter> TenantNumberCounters { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
