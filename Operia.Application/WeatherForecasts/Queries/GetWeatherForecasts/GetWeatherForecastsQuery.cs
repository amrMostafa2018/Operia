using MediatR;
using Operia.Application.WeatherForecasts.DTOs;

namespace Operia.Application.WeatherForecasts.Queries.GetWeatherForecasts;

public sealed record GetWeatherForecastsQuery : IRequest<IReadOnlyList<WeatherForecastDto>>;
