namespace Retro.FastInject.Sample.Cli.Services;

public sealed class TransientClass : IDisposable {
  public string Name => nameof(TransientClass);

  public void Dispose() => Console.WriteLine($"{nameof(TransientClass)}.Dispose()");
}