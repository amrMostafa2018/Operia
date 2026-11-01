using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operia.Domain.Entities;

namespace Operia.Infrastructure.Persistence.Configurations;

public sealed class PackageStaffConfiguration : IEntityTypeConfiguration<PackageStaff>
{
    public void Configure(EntityTypeBuilder<PackageStaff> builder)
    {
        builder.ToTable("PackageStaff");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasMaxLength(36);
        builder.Property(item => item.TenantId).IsRequired().HasMaxLength(36);
        builder.Property(item => item.PackageId).IsRequired().HasMaxLength(36);
        builder.Property(item => item.EmployeeId).IsRequired().HasMaxLength(36);
        builder.Property(item => item.CreatedBy).HasMaxLength(450);
        builder.HasIndex(item => new { item.TenantId, item.PackageId, item.EmployeeId }).IsUnique();
        builder.HasOne(item => item.Package).WithMany(package => package.AvailableStaff)
            .HasForeignKey(item => item.PackageId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Employee).WithMany()
            .HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
