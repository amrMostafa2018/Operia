using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Operia.Infrastructure.Persistence.Configurations;

public sealed class WeatherForecastConfiguration
    //: IEntityTypeConfiguration<WeatherForecast>
{
    //public void Configure(EntityTypeBuilder<WeatherForecast> builder)
    //{
    //    builder.ToTable("WeatherForecasts");

    //    builder.HasKey(x => x.Id);

    //    builder.Property(x => x.Id)
    //        .ValueGeneratedOnAdd();

    //    builder.Property(x => x.Date)
    //        .IsRequired();

    //    builder.Property(x => x.TemperatureC)
    //        .IsRequired();

    //    builder.Property(x => x.Summary)
    //        .HasMaxLength(200);

    //    builder.Property(x => x.CreatedAt)
    //        .IsRequired();

    //    builder.Property(x => x.CreatedBy)
    //        .HasMaxLength(100);

    //    builder.Property(x => x.LastModifiedBy)
    //        .HasMaxLength(100);

    //    builder.Ignore(x => x.TemperatureF);
    //}
}
