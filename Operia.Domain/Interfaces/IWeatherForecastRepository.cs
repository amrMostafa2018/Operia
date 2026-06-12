using Operia.Domain.Entities;

namespace Operia.Domain.Interfaces;

public interface IWeatherForecastRepository
{
    Task<WeatherForecast?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WeatherForecast>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(WeatherForecast entity, CancellationToken cancellationToken = default);
    void Update(WeatherForecast entity);
    void Delete(WeatherForecast entity);
}
