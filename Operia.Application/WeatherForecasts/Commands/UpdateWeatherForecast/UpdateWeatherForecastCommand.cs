using MediatR;

namespace Operia.Application.WeatherForecasts.Commands.UpdateWeatherForecast;

public sealed record UpdateWeatherForecastCommand(
    int Id,
    DateOnly Date,
    int TemperatureC,
    string? Summary) : IRequest;
