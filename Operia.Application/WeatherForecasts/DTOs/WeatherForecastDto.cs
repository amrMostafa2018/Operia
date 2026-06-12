using AutoMapper;
using Operia.Application.Common.Mappings;
using Operia.Domain.Entities;

namespace Operia.Application.WeatherForecasts.DTOs;

public sealed record WeatherForecastDto(
    int Id,
    DateOnly Date,
    int TemperatureC,
    int TemperatureF,
    string? Summary) : IMapFrom<WeatherForecast>
{
    public static void Mapping(Profile profile) =>
        profile.CreateMap<WeatherForecast, WeatherForecastDto>();
}
