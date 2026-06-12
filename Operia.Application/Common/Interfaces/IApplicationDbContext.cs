using Microsoft.EntityFrameworkCore;
using Operia.Domain.Entities;

namespace Operia.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<WeatherForecast> WeatherForecasts { get; }
}
