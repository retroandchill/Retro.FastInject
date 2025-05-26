using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Retro.FastInject.Core;

namespace Retro.FastInject.Dynamic;

/// <summary>
/// A dynamic service provider that resolves services from an IServiceCollection.
/// </summary>
public sealed class HybridServiceProvider<T> : IKeyedServiceProvider where T : ICompileTimeServiceProvider, ICompileTimeScopeFactory {
  private Scope? _rootScope;
  private readonly Dictionary<Type, List<ServiceDescriptor>> _descriptors = new();
  private readonly Dictionary<Type, Dictionary<object, List<ServiceDescriptor>>> _keyedDescriptors = new();
  private readonly Dictionary<ServiceDescriptor, object> _singletonInstances = new();
  private readonly T _compileTimeServiceProvider;


  /// <summary>
  /// Provides a hybrid service provider implementation that supports both compile-time and runtime service resolution.
  /// </summary>
  /// <param name="compileTimeServiceProvider">
  /// An instance of the compile-time service provider.
  /// </param>
  /// <param name="services">
  /// A collection of service descriptors used for runtime service registrations.
  /// </param>
  /// <exception cref="ArgumentNullException">
  /// Thrown when either <paramref name="compileTimeServiceProvider"/> or <paramref name="services"/> is null.
  /// </exception>
  public HybridServiceProvider(T compileTimeServiceProvider, IServiceCollection services) {
    ArgumentNullException.ThrowIfNull(compileTimeServiceProvider);
    ArgumentNullException.ThrowIfNull(services);

    _compileTimeServiceProvider = compileTimeServiceProvider;

    // Organize descriptors by service type and lifetime
    foreach (var descriptor in services) {

      if (descriptor.ServiceKey is not null) {
        if (!_keyedDescriptors.TryGetValue(descriptor.ServiceType, out var keyedServices)) {
          keyedServices = new Dictionary<object, List<ServiceDescriptor>>();
          _keyedDescriptors[descriptor.ServiceType] = keyedServices;
        }

        if (!keyedServices.TryGetValue(descriptor.ServiceKey, out var descriptorsList)) {
          descriptorsList = [];
          keyedServices[descriptor.ServiceKey] = descriptorsList;
        }

        descriptorsList.Add(descriptor);
      } else {
        if (!_descriptors.TryGetValue(descriptor.ServiceType, out var descriptorsList)) {
          descriptorsList = [];
          _descriptors[descriptor.ServiceType] = descriptorsList;
        }

        descriptorsList.Add(descriptor);
      }
    }
  }

  private Scope GetRootScope() {
    return LazyInitializer.EnsureInitialized(ref _rootScope, 
                                             () => CreateScope(_compileTimeServiceProvider.GetRootScope()));
  }
  
  private static bool IsCollectionType(Type type, [NotNullWhen(true)] out Type? elementType) {
    elementType = null;
    
    if (!type.IsGenericType) {
      return false;
    }
    
    var genericTypeDefinition = type.GetGenericTypeDefinition();

    if (genericTypeDefinition != typeof(IEnumerable<>) &&
        genericTypeDefinition != typeof(IReadOnlyCollection<>) &&
        genericTypeDefinition != typeof(IReadOnlyList<>) &&
        genericTypeDefinition != typeof(ImmutableArray<>)) return false;

    elementType = type.GetGenericArguments()[0];
    return true;

  }

  private static bool IsLazyType(Type type, [NotNullWhen(true)] out Type? elementType) {
    elementType = null;
    
    if (!type.IsGenericType) {
      return false;
    }
    
    var genericTypeDefinition = type.GetGenericTypeDefinition();
    
    if (genericTypeDefinition != typeof(Lazy<>)) return false;
    
    elementType = type.GetGenericArguments()[0];
    return true;
  }

  /// <summary>
  /// Gets the service object of the specified type.
  /// </summary>
  /// <param name="serviceType">The type of the service to get.</param>
  /// <returns>The service object or null if not found.</returns>
  public object? GetService(Type serviceType) {
    if (serviceType == typeof(IServiceProvider) || serviceType == typeof(IServiceScopeFactory)) {
      return this;
    }

    if (CheckCollectionInjection(this, GetRootScope(), serviceType, out var asReadOnly)) {
      return asReadOnly;
    }

    if (CheckLazyInjection(serviceType, _compileTimeServiceProvider, out var lazyService)) return lazyService;

    if (!_descriptors.TryGetValue(serviceType, out var descriptors) || descriptors.Count <= 0) return null;

    // Always use the last registered service when multiple registrations exist
    var descriptor = descriptors[^1];
    return ResolveService(descriptor, GetRootScope(), _compileTimeServiceProvider);
  }
  private static bool CheckLazyInjection(Type serviceType, 
                                         IServiceProvider context, 
                                         out object? lazyService) {
    if (!IsLazyType(serviceType, out var lazyContents)) {
      lazyService = null;
      return false;
    }

    var lazyType = typeof(Lazy<>).MakeGenericType(lazyContents);
    var funcType = typeof(Func<>).MakeGenericType(lazyContents);
    var lazyConstructor = lazyType.GetConstructor([funcType])!;
    
    var funcMethod = typeof(ServiceProviderServiceExtensions).GetMethods()
        .Where(x => x.Name == nameof(ServiceProviderServiceExtensions.GetRequiredService))
        .Select(m => new {
            Method = m,
            Params = m.GetParameters(),
            Args = m.GetGenericArguments()
        })
        .Where(x => x.Params.Length == 1
                    && x.Args.Length == 1)
        .Select(x => x.Method.MakeGenericMethod(lazyContents))
        .First();
    
    // Create a delegate that will resolve the service when needed
    var valueFactory = Delegate.CreateDelegate(funcType, context, 
        funcMethod);

    // Create the Lazy instance using the constructor and delegate
    lazyService = lazyConstructor.Invoke([valueFactory]);
    return true;
  }
  private static bool CheckCollectionInjection(HybridServiceProvider<T> serviceProvider, 
                                               Scope currentScope, Type serviceType, out object? asReadOnly) {
    asReadOnly = null;
    
    // Check if this is a collection type
    if (!IsCollectionType(serviceType, out var elementType)) {
      return false;
    }

    // Get all registered services of the element type
    if (!serviceProvider._descriptors.TryGetValue(elementType, out var descriptors) || descriptors.Count <= 0) {
      asReadOnly = elementType.CreateEmptyArray();
      return true;
    }
    
    // Create an array of all services
    var services = descriptors
        .Select(descriptor => serviceProvider.ResolveService(descriptor, currentScope, serviceProvider._compileTimeServiceProvider))
        .Where(service => service != null);

    // Return empty collection
    return ResolveFoundServices(serviceType, services, elementType, out asReadOnly);

  }
  
  private static bool ResolveFoundServices(Type serviceType, IEnumerable<object?> services, 
                                           Type elementType, out object? asReadOnly) {
    var castServices = services.CastEnumerable(serviceType);
          
    // If it's an actual collection type, return the array directly
    if (serviceType.GetGenericTypeDefinition() != typeof(IEnumerable<>)) {
      asReadOnly = castServices.CastImmutableArray(elementType);
      return true;
    }
      
    // For other collection types, return the immutable array which implements all the interfaces
    asReadOnly = castServices;
    return true;
  }

  /// <summary>
  /// Gets the service object of the specified type with the specified key.
  /// </summary>
  /// <param name="serviceType">The type of the service to get.</param>
  /// <param name="serviceKey">The key of the service to get.</param>
  /// <returns>The service object or null if not found.</returns>
  public object? GetKeyedService(Type serviceType, object? serviceKey) {
    if (serviceKey is null) {
      return GetService(serviceType);
    }

    if (!_keyedDescriptors.TryGetValue(serviceType, out var keyedServices) ||
        !keyedServices.TryGetValue(serviceKey, out var descriptors) ||
        descriptors.Count <= 0) return null;

    // Always use the last registered service when multiple registrations exist
    var descriptor = descriptors[^1];
    return ResolveService(descriptor, GetRootScope(), _compileTimeServiceProvider);
  }

  /// <summary>
  /// Gets the service object of the specified type with the specified key.
  /// </summary>
  /// <param name="serviceType">The type of the service to get.</param>
  /// <param name="serviceKey">The key of the service to get.</param>
  /// <returns>The service object.</returns>
  /// <exception cref="InvalidOperationException">Thrown if the service is not found.</exception>
  public object GetRequiredKeyedService(Type serviceType, object? serviceKey) {
    var service = GetKeyedService(serviceType, serviceKey);
    if (service is null) {
      throw new InvalidOperationException($"Service of type '{serviceType}' with key '{serviceKey}' cannot be resolved.");
    }

    return service;
  }

  /// <summary>
  /// Creates a new service scope.
  /// </summary>
  /// <returns>The service scope.</returns>
  public Scope CreateScope(ICompileTimeServiceScope compileTimeServiceScope) {
    return new Scope(this, compileTimeServiceScope);
  }

  private object? ResolveService(ServiceDescriptor descriptor, Scope currentScope, ICompileTimeServiceProvider root) {
    switch (descriptor.Lifetime) {
      case ServiceLifetime.Singleton when _singletonInstances.TryGetValue(descriptor, out var instance):
        return instance;
      case ServiceLifetime.Singleton: {
        var service = CreateServiceInstance(descriptor, currentScope.CompileTimeScope);
        if (service is null) return service;

        _singletonInstances[descriptor] = service;
        root.TryAddDisposable(service);
        return service;
      }
      case ServiceLifetime.Scoped:
        return currentScope.ResolveService(descriptor);
      case ServiceLifetime.Transient:
      default: {
        // Transient
        var service = CreateServiceInstance(descriptor, currentScope.CompileTimeScope);
        if (service is not null) {
          currentScope.TryAddDisposable(service);
        }
        return service;
      }
    }
  }

  private static object? CreateServiceInstance(ServiceDescriptor descriptor, 
                                               ICompileTimeServiceProvider currentScope) {
    Type implementationType;
    if (descriptor.ServiceKey is not null) {
      if (descriptor.KeyedImplementationFactory is not null) {
        return descriptor.KeyedImplementationFactory(currentScope, descriptor.ServiceKey);
      }
      
      if (descriptor.KeyedImplementationInstance is not null) {
        return descriptor.KeyedImplementationInstance;
      }

      if (descriptor.KeyedImplementationType is null) return null;
      implementationType = descriptor.KeyedImplementationType;
    } else {
      if (descriptor.ImplementationFactory is not null) {
        return descriptor.ImplementationFactory(currentScope);
      }
      if (descriptor.ImplementationInstance is not null) {
        return descriptor.ImplementationInstance;
      }
      
      if (descriptor.ImplementationType is null) return null;
      implementationType = descriptor.ImplementationType;
    }
    
    try {
      return ResolveConstructorParameters(currentScope, implementationType);
    } catch (Exception ex) {
      throw new InvalidOperationException($"Error resolving service '{descriptor.ServiceType}'", ex);
    }
  }
  private static object? ResolveConstructorParameters(ICompileTimeServiceProvider currentScope, Type implementationType) {
    // Find constructor with the most parameters that we can resolve
    var constructors = implementationType.GetConstructors()
        .OrderByDescending(c => c.GetParameters().Length)
        .ToList();

    foreach (var constructor in constructors) {
      var parameters = constructor.GetParameters();
      var parameterInstances = new object?[parameters.Length];
      var canResolveAll = true;

      for (var i = 0; i < parameters.Length; i++) {
        var parameter = parameters[i];

        // Check if the parameter is a keyed service
        var keyedServiceAttribute = parameter.GetCustomAttribute<FromKeyedServicesAttribute>();

        object? parameterInstance;
        if (keyedServiceAttribute is not null) {
          // Extract the key value from the attribute
          var key = keyedServiceAttribute.Key;
          parameterInstance = currentScope.GetKeyedService(parameter.ParameterType, key);
        } else {
          parameterInstance = currentScope.GetService(parameter.ParameterType);
        }

        if (parameterInstance is null && !parameter.IsOptional) {
          canResolveAll = false;
          break;
        }
          
        parameterInstances[i] = parameterInstance ?? parameter.DefaultValue;
      }

      if (canResolveAll) {
        return constructor.Invoke(parameterInstances);
      }
    }

    // If no constructor works, try to create with default constructor
    return Activator.CreateInstance(implementationType);
  }

  /// <summary>
  /// A scope that provides services from a service provider.
  /// </summary>
  public sealed class Scope : IKeyedServiceProvider {
    private readonly HybridServiceProvider<T> _hybridServiceProvider;
    private readonly Dictionary<ServiceDescriptor, object> _scopedInstances = new();
    
    internal ICompileTimeServiceScope CompileTimeScope { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Scope"/> class.
    /// </summary>
    /// <param name="hybridServiceProvider">The service provider.</param>
    /// <param name="scope">The compile time service provider's scope.</param>
    public Scope(HybridServiceProvider<T> hybridServiceProvider, ICompileTimeServiceScope scope) {
      _hybridServiceProvider = hybridServiceProvider;
      CompileTimeScope = scope;
    }

    /// <summary>
    /// Gets the service object of the specified type.
    /// </summary>
    /// <param name="serviceType">The type of the service to get.</param>
    /// <returns>The service object or null if not found.</returns>
    public object? GetService(Type serviceType) {
      if (serviceType == typeof(IServiceProvider)) {
        return this;
      }

      if (serviceType == typeof(IServiceScopeFactory)) {
        return _hybridServiceProvider;
      }

      if (CheckCollectionInjection(_hybridServiceProvider, this, serviceType, out var asReadOnly)) {
        return asReadOnly;
      }
      
      if (CheckLazyInjection(serviceType, CompileTimeScope, out var lazyService)) return lazyService;

      if (!_hybridServiceProvider._descriptors.TryGetValue(serviceType, out var descriptors) || descriptors.Count <= 0) return null;

      // Always use the last registered service when multiple registrations exist
      var descriptor = descriptors[^1];
      return _hybridServiceProvider.ResolveService(descriptor, this, _hybridServiceProvider._compileTimeServiceProvider);
    }

    /// <summary>
    /// Gets the service object of the specified type with the specified key.
    /// </summary>
    /// <param name="serviceType">The type of the service to get.</param>
    /// <param name="serviceKey">The key of the service to get.</param>
    /// <returns>The service object or null if not found.</returns>
    public object? GetKeyedService(Type serviceType, object? serviceKey) {
      if (serviceKey is null) {
        return GetService(serviceType);
      }

      if (!_hybridServiceProvider._keyedDescriptors.TryGetValue(serviceType, out var keyedServices) ||
          !keyedServices.TryGetValue(serviceKey, out var descriptors) ||
          descriptors.Count <= 0) return null;

      // Always use the last registered service when multiple registrations exist
      var descriptor = descriptors[^1];
      return _hybridServiceProvider.ResolveService(descriptor, this, _hybridServiceProvider._compileTimeServiceProvider);
    }

    /// <summary>
    /// Gets the service object of the specified type with the specified key.
    /// </summary>
    /// <param name="serviceType">The type of the service to get.</param>
    /// <param name="serviceKey">The key of the service to get.</param>
    /// <returns>The service object.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the service is not found.</exception>
    public object GetRequiredKeyedService(Type serviceType, object? serviceKey) {
      var service = GetKeyedService(serviceType, serviceKey);
      if (service is null) {
        throw new InvalidOperationException($"Service of type '{serviceType}' with key '{serviceKey}' cannot be resolved.");
      }

      return service;
    }

    internal object? ResolveService(ServiceDescriptor descriptor) {
      if (_scopedInstances.TryGetValue(descriptor, out var instance)) {
        return instance;
      }

      var service = CreateServiceInstance(descriptor, CompileTimeScope);
      if (service is null) return service;

      _scopedInstances[descriptor] = service;
      CompileTimeScope.TryAddDisposable(service);
      return service;
    }
    
    internal void TryAddDisposable(object instance) {
      CompileTimeScope.TryAddDisposable(instance);
    }
  }
}