using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operia.Domain.Entities;

namespace Operia.Infrastructure.Persistence.Configurations;

public sealed class SaleCollectionConfiguration : IEntityTypeConfiguration<SaleCollection>
{
    public void Configure(EntityTypeBuilder<SaleCollection> builder)
    {
        builder.ToTable("SaleCollections");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(36);
        builder.Property(x => x.TenantId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.SaleId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(100);
        builder.Property(x => x.PaymentMethod).IsRequired().HasMaxLength(30);
        builder.Property(x => x.CollectedByUserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.ReviewStatus).HasMaxLength(20);
        builder.Property(x => x.ProofContentType).HasMaxLength(50);
        builder.Property(x => x.ReviewedByUserId).HasMaxLength(450);
        builder.Property(x => x.CreatedBy).HasMaxLength(450);
        builder.HasIndex(x => new { x.TenantId, x.SaleId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.SaleId })
            .IsUnique().HasFilter("[Confirmed] = 1");
        builder.HasOne<Sale>().WithMany().HasForeignKey(x => x.SaleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
