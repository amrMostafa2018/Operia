using MediatR;
using Operia.Domain.Entities;
using Operia.Domain.Interfaces;

namespace Operia.Application.WeatherForecasts.Commands.CreateWeatherForecast;

public sealed class CreateWeatherForecastCommandHandler
    : IRequestHandler<CreateWeatherForecastCommand, int>
{
    private readonly IWeatherForecastRepository _repository;

    public CreateWeatherForecastCommandHandler(IWeatherForecastRepository repository)
    {
        _repository = repository;
    }

    public async Task<int> Handle(
        CreateWeatherForecastCommand request,
        CancellationToken cancellationToken)
    {
        var forecast = WeatherForecast.Create(request.Date, request.TemperatureC, request.Summary);

        await _repository.AddAsync(forecast, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return forecast.Id;
    }
}
