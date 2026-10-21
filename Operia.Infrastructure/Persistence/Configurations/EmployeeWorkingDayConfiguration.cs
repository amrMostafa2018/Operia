using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operia.Domain.Entities;

namespace Operia.Infrastructure.Persistence.Configurations;

public sealed class EmployeeWorkingDayConfiguration : IEntityTypeConfiguration<EmployeeWorkingDay>
{
    public void Configure(EntityTypeBuilder<EmployeeWorkingDay> builder)
    {
        builder.ToTable("EmployeeWorkingDays");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(36);
        builder.Property(x => x.TenantId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.EmployeeId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.BranchId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.Day).IsRequired().HasMaxLength(3);
        builder.Property(x => x.CreatedBy).HasMaxLength(450);
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.BranchId, x.Day }).IsUnique();
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
