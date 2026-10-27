using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operia.Domain.Entities;

namespace Operia.Infrastructure.Persistence.Configurations;

public sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales");
        builder.HasKey(sale => sale.Id);
        builder.Property(sale => sale.Id).HasMaxLength(36);
        builder.Property(sale => sale.TenantId).IsRequired().HasMaxLength(36);
        builder.Property(sale => sale.BranchId).IsRequired().HasMaxLength(36);
        builder.Property(sale => sale.CustomerId).IsRequired().HasMaxLength(36);
        builder.Property(sale => sale.IdempotencyKey).IsRequired().HasMaxLength(100);
        builder.Property(sale => sale.RequestHash).IsRequired().HasMaxLength(64);
        builder.Property(sale => sale.PaymentMethod).IsRequired().HasMaxLength(30);
        builder.Property(sale => sale.Status).IsRequired().HasMaxLength(40);
        builder.Property(sale => sale.GrandTotal).HasPrecision(18, 2);
        builder.Property(sale => sale.DueNow).HasPrecision(18, 2);
        builder.Property(sale => sale.ItemsSnapshotJson).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(sale => sale.CreatedBy).HasMaxLength(450);
        builder.HasIndex(sale => new { sale.TenantId, sale.IdempotencyKey }).IsUnique();
        builder.HasIndex(sale => new { sale.TenantId, sale.CustomerId, sale.CreatedAt });
        builder.HasOne<Customer>().WithMany().HasForeignKey(sale => sale.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(sale => sale.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
