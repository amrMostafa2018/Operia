using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operia.Domain.Entities;

namespace Operia.Infrastructure.Persistence.Configurations;

public sealed class PlatformRevenueConfiguration : IEntityTypeConfiguration<PlatformRevenue>
{
    public void Configure(EntityTypeBuilder<PlatformRevenue> builder)
    {
        builder.ToTable("PlatformRevenues");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasMaxLength(36);

        builder.Property(x => x.TenantId)
            .IsRequired()
            .HasMaxLength(36);

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2);

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.ScreenShotUrl)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.PlatformRevenues)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
