using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operia.Domain.Entities;

namespace Operia.Infrastructure.Persistence.Configurations;

public sealed class BusinessSettingsConfiguration : IEntityTypeConfiguration<BusinessSettings>
{
    public void Configure(EntityTypeBuilder<BusinessSettings> builder)
    {
        builder.ToTable("BusinessSettings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(36);
        builder.Property(x => x.BusinessId).IsRequired().HasMaxLength(36);
        builder.HasIndex(x => x.BusinessId).IsUnique();
        builder.Property(x => x.PaymentMethodsJson).IsRequired();
        builder.Property(x => x.WorkingDaysJson).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(450);
        builder.HasOne(x => x.Business).WithOne(x => x.Settings)
            .HasForeignKey<BusinessSettings>(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
