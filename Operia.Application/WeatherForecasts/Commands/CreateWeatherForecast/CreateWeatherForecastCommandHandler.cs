using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Entities;
using Operia.Domain.Interfaces;

namespace Operia.Application.WeatherForecasts.Commands.CreateWeatherForecast;

public sealed class CreateWeatherForecastCommandHandler
    : IRequestHandler<CreateWeatherForecastCommand, int>
{
    private readonly IWeatherForecastRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateWeatherForecastCommandHandler(
        IWeatherForecastRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<int> Handle(
        CreateWeatherForecastCommand request,
        CancellationToken cancellationToken)
    {
        var forecast = WeatherForecast.Create(request.Date, request.TemperatureC, request.Summary);

        await _repository.AddAsync(forecast, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return forecast.Id;
    }
}
