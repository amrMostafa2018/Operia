using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operia.Domain.Entities;

namespace Operia.Infrastructure.Persistence.Configurations;

/// <summary>Configures persistence and relationships for booking records.</summary>
public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    /// <summary>Defines database mapping, constraints, and relationships for this entity.</summary>
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(36);
        builder.Property(x => x.TenantId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.BookingNumber).IsRequired().HasMaxLength(32);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(100);
        builder.Property(x => x.BranchId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.EmployeeId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.CustomerId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.CustomerName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.CustomerMobile).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Source).IsRequired().HasMaxLength(30);
        builder.Property(x => x.PaymentMethod).HasMaxLength(30);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.PaidAmount).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.CreatedBy).HasMaxLength(450);
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => new { x.TenantId, x.BookingNumber }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.ScheduledDate, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.BranchId, x.ScheduledDate });
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
