using MediatR;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.WeatherForecasts.Commands.CreateWeatherForecast;
using Operia.Application.WeatherForecasts.Commands.DeleteWeatherForecast;
using Operia.Application.WeatherForecasts.Commands.UpdateWeatherForecast;
using Operia.Application.WeatherForecasts.DTOs;
using Operia.Application.WeatherForecasts.Queries.GetWeatherForecastById;
using Operia.Application.WeatherForecasts.Queries.GetWeatherForecasts;

namespace Operia.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class WeatherForecastsController : ControllerBase
{
    private readonly IMediator _mediator;

    public WeatherForecastsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WeatherForecastDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetWeatherForecastsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(WeatherForecastDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetWeatherForecastByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateWeatherForecastCommand command,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateWeatherForecastCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.Id)
            return BadRequest("Route id does not match body id.");

        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteWeatherForecastCommand(id), cancellationToken);
        return NoContent();
    }
}
