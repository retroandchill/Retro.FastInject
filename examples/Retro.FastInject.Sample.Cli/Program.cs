using DotMake.CommandLine;
using Retro.FastInject.Sample.Cli;
using Retro.FastInject.Sample.Cli.Services;

Cli.Ext.SetServiceProvider(new CliServiceProvider());
await Cli.RunAsync<RootCliCommand>();