using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operia.Domain.Entities;

namespace Operia.Infrastructure.Persistence.Configurations;

public sealed class PackageConfiguration : IEntityTypeConfiguration<Package>
{
    public void Configure(EntityTypeBuilder<Package> builder)
    {
        builder.ToTable("Packages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(36);
        builder.Property(x => x.TenantId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.BusinessId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(250);
        builder.Property(x => x.ServiceCategoryId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.SubServiceCategoryId).HasMaxLength(36);
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.DiscountCode).HasMaxLength(50);
        builder.Property(x => x.DiscountPercent).HasPrecision(5, 2);
        builder.Property(x => x.CreatedBy).HasMaxLength(450);
        builder.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ServiceCategory)
            .WithMany(x => x.Packages)
            .HasForeignKey(x => x.ServiceCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SubServiceCategory)
            .WithMany(x => x.Packages)
            .HasForeignKey(x => x.SubServiceCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
