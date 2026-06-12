using MediatR;
using Operia.Domain.Exceptions;
using Operia.Domain.Interfaces;

namespace Operia.Application.WeatherForecasts.Commands.UpdateWeatherForecast;

public sealed class UpdateWeatherForecastCommandHandler
    : IRequestHandler<UpdateWeatherForecastCommand>
{
    private readonly IWeatherForecastRepository _repository;

    public UpdateWeatherForecastCommandHandler(IWeatherForecastRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(
        UpdateWeatherForecastCommand request,
        CancellationToken cancellationToken)
    {
        var forecast = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.WeatherForecast), request.Id);

        forecast.Update(request.Date, request.TemperatureC, request.Summary);

        _repository.Update(forecast);
        await _repository.SaveChangesAsync(cancellationToken);
    }
}
