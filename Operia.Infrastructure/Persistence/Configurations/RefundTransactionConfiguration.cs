using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operia.Domain.Entities;

namespace Operia.Infrastructure.Persistence.Configurations;

public sealed class RefundTransactionConfiguration : IEntityTypeConfiguration<RefundTransaction>
{
    public void Configure(EntityTypeBuilder<RefundTransaction> builder)
    {
        builder.ToTable("RefundTransactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(36);
        builder.Property(x => x.TenantId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.CustomerPackageId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.SaleId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.CustomerId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.RecordedByUserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(x => new { x.TenantId, x.CustomerPackageId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.CustomerPackageId, x.RecordedAtUtc });
        builder.HasOne<CustomerPackage>().WithMany().HasForeignKey(x => x.CustomerPackageId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Sale>().WithMany().HasForeignKey(x => x.SaleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
