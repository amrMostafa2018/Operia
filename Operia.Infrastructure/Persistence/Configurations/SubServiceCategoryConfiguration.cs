using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operia.Domain.Entities;

namespace Operia.Infrastructure.Persistence.Configurations;

public sealed class SubServiceCategoryConfiguration : IEntityTypeConfiguration<SubServiceCategory>
{
    public void Configure(EntityTypeBuilder<SubServiceCategory> builder)
    {
        builder.ToTable("SubServiceCategories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(36);
        builder.Property(x => x.TenantId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.BusinessId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.ServiceCategoryId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.CreatedBy).HasMaxLength(450);
        builder.HasIndex(x => new { x.BusinessId, x.ServiceCategoryId, x.Name });
        builder.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ServiceCategory)
            .WithMany(x => x.SubServiceCategories)
            .HasForeignKey(x => x.ServiceCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
