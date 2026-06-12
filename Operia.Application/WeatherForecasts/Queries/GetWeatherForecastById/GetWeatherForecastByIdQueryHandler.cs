using MediatR;
using Operia.Application.WeatherForecasts.DTOs;
using Operia.Domain.Exceptions;
using Operia.Domain.Interfaces;

namespace Operia.Application.WeatherForecasts.Queries.GetWeatherForecastById;

public sealed class GetWeatherForecastByIdQueryHandler
    : IRequestHandler<GetWeatherForecastByIdQuery, WeatherForecastDto>
{
    private readonly IWeatherForecastRepository _repository;

    public GetWeatherForecastByIdQueryHandler(IWeatherForecastRepository repository)
    {
        _repository = repository;
    }

    public async Task<WeatherForecastDto> Handle(
        GetWeatherForecastByIdQuery request,
        CancellationToken cancellationToken)
    {
        var forecast = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(WeatherForecastDto), request.Id);

        return WeatherForecastDto.FromEntity(forecast);
    }
}
