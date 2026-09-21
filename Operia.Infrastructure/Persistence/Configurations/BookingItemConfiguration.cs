using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operia.Domain.Entities;

namespace Operia.Infrastructure.Persistence.Configurations;

/// <summary>Configures persistence and relationships for booking item records.</summary>
public sealed class BookingItemConfiguration : IEntityTypeConfiguration<BookingItem>
{
    /// <summary>Defines database mapping, constraints, and relationships for this entity.</summary>
    public void Configure(EntityTypeBuilder<BookingItem> builder)
    {
        builder.ToTable("BookingItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(36);
        builder.Property(x => x.TenantId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.BookingId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.PackageId).HasMaxLength(36);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.CreatedBy).HasMaxLength(450);
        builder.HasOne(x => x.Booking).WithMany(x => x.Items).HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Package).WithMany().HasForeignKey(x => x.PackageId).OnDelete(DeleteBehavior.Restrict);
    }
}
