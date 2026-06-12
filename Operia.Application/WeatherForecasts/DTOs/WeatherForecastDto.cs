using Operia.Domain.Entities;

namespace Operia.Application.WeatherForecasts.DTOs;

public sealed record WeatherForecastDto(
    int Id,
    DateOnly Date,
    int TemperatureC,
    int TemperatureF,
    string? Summary)
{
    public static WeatherForecastDto FromEntity(WeatherForecast entity) =>
        new(entity.Id, entity.Date, entity.TemperatureC, entity.TemperatureF, entity.Summary);
}
