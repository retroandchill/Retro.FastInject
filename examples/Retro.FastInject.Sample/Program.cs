using System;
using Microsoft.Extensions.DependencyInjection;
using Retro.FastInject.Sample;
using Retro.FastInject.Sample.Services;

var serviceProvider = new SampleServiceProvider();
var singleton = serviceProvider.GetService<ISingletonService>();
using var scope = serviceProvider.CreateScope();
var scopedService = serviceProvider.GetService<IScopedService>();
Console.WriteLine("Hello World!");