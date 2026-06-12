using Microsoft.EntityFrameworkCore;
using Operia.Domain.Entities;
using Operia.Domain.Interfaces;
using Operia.Infrastructure.Persistence;

namespace Operia.Infrastructure.Repositories;

public sealed class WeatherForecastRepository : IWeatherForecastRepository
{
    private readonly ApplicationDbContext _context;

    public WeatherForecastRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WeatherForecast?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await _context.WeatherForecasts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<WeatherForecast>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.WeatherForecasts
            .AsNoTracking()
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(WeatherForecast entity, CancellationToken cancellationToken = default)
        => await _context.WeatherForecasts.AddAsync(entity, cancellationToken);

    public void Update(WeatherForecast entity)
        => _context.WeatherForecasts.Update(entity);

    public void Delete(WeatherForecast entity)
        => _context.WeatherForecasts.Remove(entity);
}
