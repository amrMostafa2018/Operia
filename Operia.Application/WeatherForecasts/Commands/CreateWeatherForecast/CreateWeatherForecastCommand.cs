using MediatR;

namespace Operia.Application.WeatherForecasts.Commands.CreateWeatherForecast;

public sealed record CreateWeatherForecastCommand(
    DateOnly Date,
    int TemperatureC,
    string? Summary) : IRequest<int>;
