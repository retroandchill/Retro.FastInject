using System;
using Microsoft.Extensions.DependencyInjection;
using Retro.FastInject.Core;
using Retro.FastInject.Sample;
using Retro.FastInject.Sample.Services;

var serviceCollection = new ServiceCollection();
serviceCollection.AddSingleton<IDynamicService, DynamicService>();
using var serviceProvider = new SampleServiceProvider(4, 5.0f, serviceCollection);
var singleton = serviceProvider.GetService<ISingletonService>();
using var scope = serviceProvider.CreateScope();
var scopedService = scope.ServiceProvider.GetService<IScopedService>();
var valueService = scope.ServiceProvider.GetService<ValueService>();
var transientService = valueService.TransientService.Value;
Console.WriteLine("Hello World!");