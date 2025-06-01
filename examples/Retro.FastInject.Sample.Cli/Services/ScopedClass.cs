namespace Retro.FastInject.Sample.Cli.Services;

public sealed class ScopedClass : IDisposable {
  public string Name => nameof(ScopedClass);

  public void Dispose() => Console.WriteLine($"{nameof(ScopedClass)}.Dispose()");
}