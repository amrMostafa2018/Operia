using FluentValidation;

namespace Operia.Application.WeatherForecasts.Commands.CreateWeatherForecast;

public sealed class CreateWeatherForecastCommandValidator
    : AbstractValidator<CreateWeatherForecastCommand>
{
    public CreateWeatherForecastCommandValidator()
    {
        RuleFor(x => x.Date)
            .NotEmpty()
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date must be today or in the future.");

        RuleFor(x => x.TemperatureC)
            .InclusiveBetween(-100, 100)
            .WithMessage("Temperature must be between -100°C and 100°C.");

        RuleFor(x => x.Summary)
            .MaximumLength(200)
            .When(x => x.Summary is not null);
    }
}
