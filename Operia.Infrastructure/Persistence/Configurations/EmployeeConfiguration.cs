using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operia.Domain.Entities;
using Operia.Infrastructure.Identity;

namespace Operia.Infrastructure.Persistence.Configurations;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(36);
        builder.Property(x => x.TenantId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.IdentityUserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(8);
        builder.Property(x => x.FullName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(256);
        builder.Property(x => x.MobileNumber).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Specialty).HasMaxLength(200);
        builder.Property(x => x.JobTitle).HasMaxLength(200);
        builder.Property(x => x.PhotoUrl).HasMaxLength(500);
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => x.IdentityUserId).IsUnique();
        builder.HasOne<ApplicationUser>().WithOne().HasForeignKey<Employee>(x => x.IdentityUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UserBranchConfiguration : IEntityTypeConfiguration<UserBranch>
{
    public void Configure(EntityTypeBuilder<UserBranch> builder)
    {
        builder.ToTable("UserBranches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(36);
        builder.Property(x => x.TenantId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.EmployeeId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.BranchId).IsRequired().HasMaxLength(36);
        builder.HasIndex(x => new { x.EmployeeId, x.BranchId }).IsUnique();
        builder.HasOne(x => x.Employee).WithMany(x => x.UserBranches).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TenantNumberCounterConfiguration : IEntityTypeConfiguration<TenantNumberCounter>
{
    public void Configure(EntityTypeBuilder<TenantNumberCounter> builder)
    {
        builder.ToTable("TenantNumberCounters");
        builder.HasKey(x => x.TenantId);
        builder.Property(x => x.TenantId).HasMaxLength(36);
        builder.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}
