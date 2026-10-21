using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operia.Domain.Entities;

namespace Operia.Infrastructure.Persistence.Configurations;

public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(36);
        builder.Property(x => x.TenantId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.BusinessId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Address).IsRequired().HasMaxLength(500);
        builder.Property(x => x.PhoneNumber).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Latitude).HasPrecision(9, 6);
        builder.Property(x => x.Longitude).HasPrecision(9, 6);
        builder.Property(x => x.GoogleMapsUrl).IsRequired().HasMaxLength(500);
        builder.Property(x => x.CreatedBy).HasMaxLength(450);
        builder.HasIndex(x => new { x.BusinessId, x.Name }).IsUnique();
        builder.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Business).WithMany(x => x.Branches).HasForeignKey(x => x.BusinessId).OnDelete(DeleteBehavior.Cascade);
    }
}
