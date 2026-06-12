using MediatR;

namespace Operia.Application.WeatherForecasts.Commands.DeleteWeatherForecast;

public sealed record DeleteWeatherForecastCommand(int Id) : IRequest;
