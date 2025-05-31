using Retro.ReadOnlyParams.Annotations;
namespace Retro.FastInject.Sample.WebApi.Services;

public class WeatherForcastService([ReadOnly] ILogger<WeatherForcastService> logger) : IWeatherForcastService {
  private static readonly string[] Summaries = [
      "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
  ];
  

  public IEnumerable<WeatherForecast> GetWeatherForecasts() {
    logger.LogInformation("GetWeatherForecasts");
    return Enumerable.Range(1, 5).Select(index => new WeatherForecast {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        })
        .ToArray();
  }
}