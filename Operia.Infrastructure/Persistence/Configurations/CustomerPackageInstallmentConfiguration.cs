using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operia.Domain.Entities;

namespace Operia.Infrastructure.Persistence.Configurations;

public sealed class CustomerPackageInstallmentConfiguration : IEntityTypeConfiguration<CustomerPackageInstallment>
{
    public void Configure(EntityTypeBuilder<CustomerPackageInstallment> builder)
    {
        builder.ToTable("CustomerPackageInstallments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(36);
        builder.Property(x => x.TenantId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.CustomerPackageId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.PaymentMethod).HasMaxLength(30);
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasOne(x => x.CustomerPackage).WithMany(x => x.Installments)
            .HasForeignKey(x => x.CustomerPackageId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.TenantId, x.CustomerPackageId, x.Number }).IsUnique();
    }
}
