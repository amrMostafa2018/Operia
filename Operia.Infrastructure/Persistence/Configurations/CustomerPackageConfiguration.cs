using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operia.Domain.Entities;

namespace Operia.Infrastructure.Persistence.Configurations;

/// <summary>Configures persistence and relationships for customer package records.</summary>
public sealed class CustomerPackageConfiguration : IEntityTypeConfiguration<CustomerPackage>
{
    /// <summary>Defines database mapping, constraints, and relationships for this entity.</summary>
    public void Configure(EntityTypeBuilder<CustomerPackage> builder)
    {
        builder.ToTable("CustomerPackages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(36);
        builder.Property(x => x.TenantId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.CustomerId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.PackageId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.CreatedBy).HasMaxLength(450);
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => new { x.TenantId, x.CustomerId, x.IsActive });
        builder.HasOne(x => x.Customer).WithMany(x => x.Packages).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Package).WithMany().HasForeignKey(x => x.PackageId).OnDelete(DeleteBehavior.Restrict);
    }
}
