using AutoMapper;
using MediatR;
using Operia.Application.WeatherForecasts.DTOs;
using Operia.Domain.Exceptions;

namespace Operia.Application.WeatherForecasts.Queries.GetWeatherForecastById;

public sealed class GetWeatherForecastByIdQueryHandler
   // : IRequestHandler<GetWeatherForecastByIdQuery, WeatherForecastDto>
{
    //private readonly IWeatherForecastRepository _repository;
    //private readonly IMapper _mapper;

    //public GetWeatherForecastByIdQueryHandler(
    //    IWeatherForecastRepository repository,
    //    IMapper mapper)
    //{
    //    _repository = repository;
    //    _mapper = mapper;
    //}

    //public async Task<WeatherForecastDto> Handle(
    //    GetWeatherForecastByIdQuery request,
    //    CancellationToken cancellationToken)
    //{
    //    var forecast = await _repository.GetByIdAsync(request.Id, cancellationToken)
    //        ?? throw new NotFoundException(nameof(WeatherForecastDto), request.Id);

    //    return _mapper.Map<WeatherForecastDto>(forecast);
    //}
}
