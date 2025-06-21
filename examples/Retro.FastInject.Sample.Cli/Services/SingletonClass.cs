namespace Retro.FastInject.Sample.Cli.Services;

public sealed class SingletonClass : IDisposable {
  public string Name => nameof(SingletonClass);

  public void Dispose() => Console.WriteLine($"{nameof(SingletonClass)}.Dispose()");
}