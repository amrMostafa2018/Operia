using Operia.Domain.Common;

namespace Operia.Domain.Entities;

public sealed class WeatherForecast : AuditableEntity
{
    public DateOnly Date { get; private set; }
    public int TemperatureC { get; private set; }
    public string? Summary { get; private set; }

    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

    private WeatherForecast() { }

    public static WeatherForecast Create(DateOnly date, int temperatureC, string? summary)
    {
        return new WeatherForecast
        {
            Date = date,
            TemperatureC = temperatureC,
            Summary = summary
        };
    }

    public void Update(DateOnly date, int temperatureC, string? summary)
    {
        Date = date;
        TemperatureC = temperatureC;
        Summary = summary;
    }
}
