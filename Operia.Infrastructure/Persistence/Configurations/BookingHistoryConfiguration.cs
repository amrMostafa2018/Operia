using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operia.Domain.Entities;

namespace Operia.Infrastructure.Persistence.Configurations;

/// <summary>Configures persistence and relationships for booking history records.</summary>
public sealed class BookingHistoryConfiguration : IEntityTypeConfiguration<BookingHistory>
{
    /// <summary>Defines database mapping, constraints, and relationships for this entity.</summary>
    public void Configure(EntityTypeBuilder<BookingHistory> builder)
    {
        builder.ToTable("BookingHistory");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(36);
        builder.Property(x => x.TenantId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.BookingId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ChangedByUserId).HasMaxLength(450);
        builder.Property(x => x.ChangedByDisplayName).HasMaxLength(200);
        builder.Property(x => x.ChangesJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.CreatedBy).HasMaxLength(450);
        builder.HasIndex(x => new { x.TenantId, x.BookingId, x.CreatedAt });
        builder.HasOne(x => x.Booking).WithMany(x => x.History).HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
    }
}
