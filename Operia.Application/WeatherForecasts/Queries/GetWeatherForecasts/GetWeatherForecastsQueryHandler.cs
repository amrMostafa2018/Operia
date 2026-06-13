using AutoMapper;
using MediatR;
using Operia.Application.WeatherForecasts.DTOs;

namespace Operia.Application.WeatherForecasts.Queries.GetWeatherForecasts;

public sealed class GetWeatherForecastsQueryHandler
   // : IRequestHandler<GetWeatherForecastsQuery, IReadOnlyList<WeatherForecastDto>>
{
    //private readonly IWeatherForecastRepository _repository;
    //private readonly IMapper _mapper;

    //public GetWeatherForecastsQueryHandler(
    //    IWeatherForecastRepository repository,
    //    IMapper mapper)
    //{
    //    _repository = repository;
    //    _mapper = mapper;
    //}

    //public async Task<IReadOnlyList<WeatherForecastDto>> Handle(
    //    GetWeatherForecastsQuery request,
    //    CancellationToken cancellationToken)
    //{
    //    var forecasts = await _repository.GetAllAsync(cancellationToken);
    //    return _mapper.Map<IReadOnlyList<WeatherForecastDto>>(forecasts);
    //}
}
