using Microsoft.AspNetCore.Mvc;
using Retro.FastInject.Sample.WebApi.Services;
using Retro.ReadOnlyParams.Annotations;

namespace Retro.FastInject.Sample.WebApi.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController([ReadOnly] IWeatherForcastService weatherForcastService) : ControllerBase {

  [HttpGet(Name = "GetWeatherForecast")]
  public IEnumerable<WeatherForecast> Get() {
    return weatherForcastService.GetWeatherForecasts();
  }
}