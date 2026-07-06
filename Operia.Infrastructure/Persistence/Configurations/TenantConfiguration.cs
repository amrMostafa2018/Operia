using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operia.Domain.Entities;

namespace Operia.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasMaxLength(36);

        builder.Property(x => x.OwnerUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.HasIndex(x => x.OwnerUserId)
            .IsUnique();

        builder.Property(x => x.BusinessName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.CountryCode)
            .IsRequired()
            .HasMaxLength(2);

        builder.Property(x => x.City)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.CurrencyCode)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.Timezone)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Balance)
            .HasPrecision(18, 2);

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(450);

        builder.Property(x => x.BusinessType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(50);
    }
}
