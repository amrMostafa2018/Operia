using MediatR;
using Operia.Application.WeatherForecasts.DTOs;
using Operia.Domain.Interfaces;

namespace Operia.Application.WeatherForecasts.Queries.GetWeatherForecasts;

public sealed class GetWeatherForecastsQueryHandler
    : IRequestHandler<GetWeatherForecastsQuery, IReadOnlyList<WeatherForecastDto>>
{
    private readonly IWeatherForecastRepository _repository;

    public GetWeatherForecastsQueryHandler(IWeatherForecastRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<WeatherForecastDto>> Handle(
        GetWeatherForecastsQuery request,
        CancellationToken cancellationToken)
    {
        var forecasts = await _repository.GetAllAsync(cancellationToken);
        return forecasts.Select(WeatherForecastDto.FromEntity).ToList();
    }
}
