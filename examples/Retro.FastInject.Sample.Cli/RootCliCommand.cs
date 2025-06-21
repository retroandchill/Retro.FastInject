using DotMake.CommandLine;
using Retro.FastInject.Sample.Cli.Services;
using Retro.ReadOnlyParams.Annotations;

namespace Retro.FastInject.Sample.Cli;

[CliCommand(Description = "The root command")]
public class RootCliCommand([ReadOnly] SingletonClass singletonDisposable,
                            [ReadOnly] ScopedClass scopedDisposable,
                            [ReadOnly] TransientClass transientDisposable) {
  
  [CliArgument(Description = "The first argument")]
  public string Argument1 { get; set; }
  
  [CliOption(Description = "The first option")]
  public string Option1 { get; set; }
  
  public void Run() {
    Console.WriteLine($@"Handler for '{GetType().FullName}' is run:");
    Console.WriteLine($@"Value for {nameof(Option1)} parameter is '{Option1}'");
    Console.WriteLine($@"Value for {nameof(Argument1)} parameter is '{Argument1}'");
    Console.WriteLine();

    Console.WriteLine($"Instance for {transientDisposable.Name} is available");
    Console.WriteLine($"Instance for {scopedDisposable.Name} is available");
    Console.WriteLine($"Instance for {singletonDisposable.Name} is available");
    Console.WriteLine();
  }

  [CliCommand(Description = "A nested level 1 sub-command which accesses the root command")]
  public class SubCommand([ReadOnly] TransientClass transientDisposable) {
    
    public void Run() {
      Console.WriteLine($@"Handler for '{GetType().FullName}' is run:");
      Console.WriteLine($"Instance for {transientDisposable.Name} is available");
    }
  }
  
}