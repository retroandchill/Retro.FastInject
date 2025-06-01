using ConsoleAppFramework;
using Retro.FastInject.Sample.Cli;
using Retro.FastInject.Sample.Cli.Services;

var serviceProvider = new CliServiceProvider();
var app = ConsoleApp.Create();
app.Add<RootCliCommand>();

await app.RunAsync(args);