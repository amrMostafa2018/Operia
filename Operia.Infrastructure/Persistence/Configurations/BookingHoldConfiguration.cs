using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operia.Domain.Entities;

namespace Operia.Infrastructure.Persistence.Configurations;

/// <summary>Configures persistence and relationships for booking hold records.</summary>
public sealed class BookingHoldConfiguration : IEntityTypeConfiguration<BookingHold>
{
    /// <summary>Defines database mapping, constraints, and relationships for this entity.</summary>
    public void Configure(EntityTypeBuilder<BookingHold> builder)
    {
        builder.ToTable("BookingHolds");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(36);
        builder.Property(x => x.TenantId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.BranchId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.EmployeeId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.CustomerId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ConvertedBookingId).HasMaxLength(36);
        builder.Property(x => x.CreatedBy).HasMaxLength(450);
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.ScheduledDate, x.ExpiresAtUtc });
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ConvertedBooking).WithMany().HasForeignKey(x => x.ConvertedBookingId).OnDelete(DeleteBehavior.Restrict);
    }
}
