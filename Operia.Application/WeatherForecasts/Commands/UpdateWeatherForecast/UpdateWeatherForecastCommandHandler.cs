using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Exceptions;
using Operia.Domain.Interfaces;

namespace Operia.Application.WeatherForecasts.Commands.UpdateWeatherForecast;

public sealed class UpdateWeatherForecastCommandHandler
    : IRequestHandler<UpdateWeatherForecastCommand>
{
    private readonly IWeatherForecastRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateWeatherForecastCommandHandler(
        IWeatherForecastRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(
        UpdateWeatherForecastCommand request,
        CancellationToken cancellationToken)
    {
        var forecast = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.WeatherForecast), request.Id);

        forecast.Update(request.Date, request.TemperatureC, request.Summary);

        _repository.Update(forecast);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
