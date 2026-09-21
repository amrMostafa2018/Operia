using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operia.Domain.Entities;

namespace Operia.Infrastructure.Persistence.Configurations;

/// <summary>Configures persistence and relationships for customer records.</summary>
public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    /// <summary>Defines database mapping, constraints, and relationships for this entity.</summary>
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(36);
        builder.Property(x => x.TenantId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.FullName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.MobileNumber).IsRequired().HasMaxLength(30);
        builder.Property(x => x.NormalizedMobileNumber).IsRequired().HasMaxLength(30);
        builder.Property(x => x.CreatedBy).HasMaxLength(450);
        builder.HasIndex(x => new { x.TenantId, x.NormalizedMobileNumber }).IsUnique();
    }
}
