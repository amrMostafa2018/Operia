using MediatR;
using Operia.Application.WeatherForecasts.DTOs;

namespace Operia.Application.WeatherForecasts.Queries.GetWeatherForecastById;

public sealed record GetWeatherForecastByIdQuery(int Id) : IRequest<WeatherForecastDto>;
