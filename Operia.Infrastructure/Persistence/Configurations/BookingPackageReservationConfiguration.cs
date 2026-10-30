using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operia.Domain.Entities;

namespace Operia.Infrastructure.Persistence.Configurations;

/// <summary>Configures persistence and relationships for booking package reservation records.</summary>
public sealed class BookingPackageReservationConfiguration : IEntityTypeConfiguration<BookingPackageReservation>
{
    /// <summary>Defines database mapping, constraints, and relationships for this entity.</summary>
    public void Configure(EntityTypeBuilder<BookingPackageReservation> builder)
    {
        builder.ToTable("BookingPackageReservations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(36);
        builder.Property(x => x.TenantId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.BookingId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.CustomerPackageId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.CancellationAtUtc);
        builder.Property(x => x.CreatedBy).HasMaxLength(450);
        builder.HasIndex(x => new { x.TenantId, x.BookingId });
        builder.HasIndex(x => new { x.TenantId, x.CustomerPackageId, x.SessionNumber })
            .IsUnique()
            .HasFilter("[CancellationAtUtc] IS NULL");
        builder.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CustomerPackage).WithMany(x => x.Reservations).HasForeignKey(x => x.CustomerPackageId).OnDelete(DeleteBehavior.Restrict);
    }
}
