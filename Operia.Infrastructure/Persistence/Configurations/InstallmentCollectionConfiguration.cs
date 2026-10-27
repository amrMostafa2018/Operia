using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operia.Domain.Entities;

namespace Operia.Infrastructure.Persistence.Configurations;

public sealed class InstallmentCollectionConfiguration : IEntityTypeConfiguration<InstallmentCollection>
{
    public void Configure(EntityTypeBuilder<InstallmentCollection> builder)
    {
        builder.ToTable("InstallmentCollections");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(36);
        builder.Property(x => x.TenantId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.CustomerPackageId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.PaymentMethod).IsRequired().HasMaxLength(32);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(80);
        builder.Property(x => x.CollectedByUserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(x => new { x.TenantId, x.CustomerPackageId, x.InstallmentNumber }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.CustomerPackageId, x.IdempotencyKey }).IsUnique();
        builder.HasOne(x => x.CustomerPackage).WithMany().HasForeignKey(x => x.CustomerPackageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
