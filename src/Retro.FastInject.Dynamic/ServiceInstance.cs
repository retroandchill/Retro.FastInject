using System;
using Microsoft.Extensions.DependencyInjection;
namespace Retro.FastInject.Dynamic;

/// <summary>
/// Represents a unique identifier for a service in the dependency injection system.
/// </summary>
/// <remarks>
/// The <see cref="ServiceInstance"/> struct is used to uniquely identify services in the 
/// FastInject dependency injection system, particularly when dealing with dynamic service
/// registration. It combines a service type with an optional key, enabling both regular 
/// and keyed service resolution.
/// 
/// This record struct is primarily used within the hybrid service provider implementation
/// to track and resolve services that were registered dynamically rather than at compile time.
/// </remarks>
/// <param name="Type">The CLR type of the service being registered or resolved.</param>
/// <param name="Descriptor">
/// The underlying service descriptor that is being used for this registration.
/// </param>
public record struct ServiceInstance(Type Type, ServiceDescriptor Descriptor);