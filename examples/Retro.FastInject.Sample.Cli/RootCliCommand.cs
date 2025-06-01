using ConsoleAppFramework;
using Retro.FastInject.Sample.Cli.Services;
using Retro.ReadOnlyParams.Annotations;

namespace Retro.FastInject.Sample.Cli;

public class RootCliCommand([ReadOnly] SingletonClass singletonDisposable,
                            [ReadOnly] ScopedClass scopedDisposable,
                            [ReadOnly] TransientClass transientDisposable) {

  [Command("")]
  public void RootCommand([Argument]string argument1, string option1) {
    Console.WriteLine($@"Handler for '{GetType().FullName}' is run:");
    Console.WriteLine($@"Value for {nameof(option1)} parameter is '{option1}'");
    Console.WriteLine($@"Value for {nameof(argument1)} parameter is '{argument1}'");
    Console.WriteLine();

    Console.WriteLine($"Instance for {transientDisposable.Name} is available");
    Console.WriteLine($"Instance for {scopedDisposable.Name} is available");
    Console.WriteLine($"Instance for {singletonDisposable.Name} is available");
    Console.WriteLine();
  }

  public void SubCommand() {
    Console.WriteLine($@"Handler for '{GetType().FullName}' is run:");
    Console.WriteLine($"Instance for {transientDisposable.Name} is available");
  }
  
}