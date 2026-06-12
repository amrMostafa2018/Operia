using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Exceptions;
using Operia.Domain.Interfaces;

namespace Operia.Application.WeatherForecasts.Commands.DeleteWeatherForecast;

public sealed class DeleteWeatherForecastCommandHandler
    : IRequestHandler<DeleteWeatherForecastCommand>
{
    private readonly IWeatherForecastRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteWeatherForecastCommandHandler(
        IWeatherForecastRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(
        DeleteWeatherForecastCommand request,
        CancellationToken cancellationToken)
    {
        var forecast = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.WeatherForecast), request.Id);

        _repository.Delete(forecast);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
