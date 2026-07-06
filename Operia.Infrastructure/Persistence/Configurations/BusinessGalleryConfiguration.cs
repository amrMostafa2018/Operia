using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operia.Domain.Entities;

namespace Operia.Infrastructure.Persistence.Configurations;

public sealed class BusinessGalleryConfiguration : IEntityTypeConfiguration<BusinessGallery>
{
    public void Configure(EntityTypeBuilder<BusinessGallery> builder)
    {
        builder.ToTable("BusinessGalleries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasMaxLength(36);

        builder.Property(x => x.BusinessId)
            .IsRequired()
            .HasMaxLength(36);

        builder.Property(x => x.ImageUrl)
            .IsRequired();

        builder.HasOne(x => x.Business)
            .WithMany(x => x.Gallery)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
