using Microsoft.EntityFrameworkCore;
using Operia.Domain.Entities;

namespace Operia.Application.Common.Interfaces;

/// <summary>
/// Application-owned persistence gateway for OPERIA's pragmatic Clean Architecture boundary.
/// Query handlers may use its EF Core sets for simple, tenant-scoped projections, while complex
/// aggregate writes should use focused repositories and a unit of work.
/// </summary>
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
    DbSet<ServiceCategory> ServiceCategories { get; }
    DbSet<SubServiceCategory> SubServiceCategories { get; }
    DbSet<Package> Packages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
