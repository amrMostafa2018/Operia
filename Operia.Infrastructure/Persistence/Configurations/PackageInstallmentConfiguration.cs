using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operia.Domain.Entities;

namespace Operia.Infrastructure.Persistence.Configurations;

public sealed class PackageInstallmentConfiguration : IEntityTypeConfiguration<PackageInstallment>
{
    public void Configure(EntityTypeBuilder<PackageInstallment> builder)
    {
        builder.ToTable("PackageInstallments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(36);
        builder.Property(x => x.TenantId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.PackageId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.TenantId, x.PackageId, x.Number }).IsUnique();
        builder.HasOne(x => x.Package)
            .WithMany(x => x.Installments)
            .HasForeignKey(x => x.PackageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
