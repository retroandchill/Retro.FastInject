using System;
using Microsoft.Extensions.DependencyInjection;
using Retro.FastInject.Sample;
using Retro.FastInject.Sample.Services;

var serviceCollection = new ServiceCollection();
serviceCollection.AddSingleton<IDynamicService, DynamicService>();
var serviceProvider = new SampleServiceProvider(4, 5.0f, serviceCollection);
var singleton = serviceProvider.GetService<ISingletonService>();
using var scope = serviceProvider.CreateScope();
var scopedService = serviceProvider.GetService<IScopedService>();
var dynamicService = serviceProvider.GetService<IDynamicService>();
Console.WriteLine("Hello World!");