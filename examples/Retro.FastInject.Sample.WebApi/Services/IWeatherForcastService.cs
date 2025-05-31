namespace Retro.FastInject.Sample.WebApi.Services;

public interface IWeatherForcastService {
  public IEnumerable<WeatherForecast> GetWeatherForecasts();
}