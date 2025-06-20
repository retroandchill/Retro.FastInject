using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Retro.FastInject.Core;
using Retro.FastInject.Core.Exceptions;

namespace Retro.FastInject.Dynamic;

/// <summary>
/// A dynamic service provider that resolves services from an IServiceCollection.
/// </summary>
public sealed class HybridServiceProvider<T> : IKeyedServiceProvider where T : ICompileTimeServiceProvider, ICompileTimeScopeFactory {
  private Scope? _rootScope;
  private readonly Dictionary<Type, List<ServiceDescriptor>> _descriptors = new();
  private readonly Dictionary<ServiceInstance, object> _singletonInstances = new();
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
      if (!_descriptors.TryGetValue(descriptor.ServiceType, out var descriptorsList)) {
        descriptorsList = [];
        _descriptors[descriptor.ServiceType] = descriptorsList;
      }

      descriptorsList.Add(descriptor);
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

    return GetService(serviceType, GetRootScope());
  }

  private object? GetService(Type serviceType, Scope currentScope) {
    if (_descriptors.TryGetValue(serviceType, out var descriptors) && descriptors.Count > 0) {
      // Always use the last registered service when multiple registrations exist
      try {
        return descriptors
            .Select(x => ResolveService(serviceType, x, currentScope, _compileTimeServiceProvider))
            .Single();
      } catch (InvalidOperationException ex) {
        throw new DependencyResolutionException($"Multiple services of type '{serviceType}' are registered.", ex);
      }
    }

    if (!serviceType.IsGenericType) {
      return null;
    }

    if (CheckCollectionInjection(currentScope, serviceType, out var asReadOnly)) {
      return asReadOnly;
    }

    if (CheckLazyInjection(serviceType, null, _compileTimeServiceProvider, out var lazyService)) {
      return lazyService;
    }
    
    return CheckGenericInjection(serviceType, null, out var genericService) ? genericService : null;
  }
  
  private bool CheckCollectionInjection(Scope currentScope, Type serviceType, out object? asReadOnly) {
    asReadOnly = null;
    
    // Check if this is a collection type
    if (!IsCollectionType(serviceType, out var elementType)) {
      return false;
    }

    // Get all registered services of the element type
    if (!_descriptors.TryGetValue(elementType, out var descriptors) || descriptors.Count <= 0) {
      asReadOnly = elementType.CreateEmptyArray();
      return true;
    }
    
    // Create an array of all services
    var services = descriptors
        .Select(descriptor => ResolveService(serviceType, descriptor, currentScope, _compileTimeServiceProvider))
        .Where(service => service != null);

    // Return empty collection
    return ResolveFoundServices(services, elementType, out asReadOnly);

  }
  
  private static bool ResolveFoundServices(IEnumerable<object?> services, 
                                           Type elementType, out object? asReadOnly) {
    var castServices = services.CastEnumerable(elementType);
    asReadOnly = castServices.CastImmutableArray(elementType);
    return true;
  }
  
  private static bool CheckLazyInjection(Type serviceType, 
                                         object? serviceKey,
                                         IServiceProvider context, 
                                         out object? lazyService) {
    if (!IsLazyType(serviceType, out var lazyContents)) {
      lazyService = null;
      return false;
    }

    var lazyType = typeof(Lazy<>).MakeGenericType(lazyContents);
    var funcType = typeof(Func<>).MakeGenericType(lazyContents);
    var lazyConstructor = lazyType.GetConstructor([funcType])!;

    var providerType = typeof(ServiceRetriever<>).MakeGenericType(lazyContents);
    var constructor = providerType.GetConstructor([typeof(IServiceProvider), typeof(object)])!;
    var provider = constructor.Invoke([context, serviceKey]);
    var delegatingMethod = providerType.GetMethod(nameof(ServiceRetriever<object>.GetService))!;
    
    // Create a delegate that will resolve the service when needed
    var valueFactory = Delegate.CreateDelegate(funcType, provider, delegatingMethod);

    // Create the Lazy instance using the constructor and delegate
    lazyService = lazyConstructor.Invoke([valueFactory]);
    return true;
  }

  private bool CheckGenericInjection(Type serviceType, object? serviceKey, out object? genericService) {
    var unboundType = serviceType.GetGenericTypeDefinition();
    
    if (!_descriptors.TryGetValue(unboundType, out var descriptors) || descriptors.Count <= 0) {
      genericService = null;
      return false;
    }

    try {
      genericService = descriptors
          .Where(x => serviceKey is null || x.ServiceKey == serviceKey)
          .Select(descriptor => ResolveService(serviceType, descriptor, GetRootScope(), _compileTimeServiceProvider))
          .SingleOrDefault();
      return genericService is not null;
    } catch (InvalidOperationException ex) {
      throw new DependencyResolutionException($"Multiple services of type '{serviceType}' are registered.", ex);
    }
  }

  /// <summary>
  /// Gets the service object of the specified type with the specified key.
  /// </summary>
  /// <param name="serviceType">The type of the service to get.</param>
  /// <param name="serviceKey">The key of the service to get.</param>
  /// <returns>The service object or null if not found.</returns>
  public object? GetKeyedService(Type serviceType, object? serviceKey) {
    return serviceKey is null ? GetService(serviceType) : GetKeyedService(serviceType, serviceKey, GetRootScope());
  }

  private object? GetKeyedService(Type serviceType, object? serviceKey, Scope currentScope) {
    if (!_descriptors.TryGetValue(serviceType, out var keyedServices)) return null;

    // If we can't isolate to a single service, we should fail
    try {
      return keyedServices
          .Where(x => x.IsKeyedService && (x.ServiceKey?.Equals(serviceKey) ?? false))
          .Select(x => ResolveService(serviceType, x, currentScope, _compileTimeServiceProvider))
          .SingleOrDefault();
    } catch (InvalidOperationException ex) {
      throw new DependencyResolutionException($"Multiple services of type '{serviceType}' with key '{serviceKey}' are registered.", ex);
    }
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
      throw new DependencyResolutionException($"Service of type '{serviceType}' with key '{serviceKey}' cannot be resolved.");
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

  private object? ResolveService(Type targetType, ServiceDescriptor descriptor, Scope currentScope, ICompileTimeServiceProvider root) {
    var serviceKey = new ServiceInstance(targetType, descriptor);
    switch (descriptor.Lifetime) {
      case ServiceLifetime.Singleton when _singletonInstances.TryGetValue(serviceKey, out var instance):
        return instance;
      case ServiceLifetime.Singleton: {
        var service = CreateServiceInstance(targetType, descriptor, currentScope.CompileTimeScope);
        if (service is null) return service;

        _singletonInstances[serviceKey] = service;
        root.TryAddDisposable(service);
        return service;
      }
      case ServiceLifetime.Scoped:
        return currentScope.ResolveService(targetType, descriptor);
      case ServiceLifetime.Transient:
      default: {
        // Transient
        var service = CreateServiceInstance(targetType, descriptor, currentScope.CompileTimeScope);
        if (service is not null) {
          currentScope.TryAddDisposable(service);
        }
        return service;
      }
    }
  }

  private static object? CreateServiceInstance(Type serviceType,
                                               ServiceDescriptor descriptor, 
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
      implementationType = SpecializeIfNeeded(serviceType, descriptor.KeyedImplementationType);
    } else {
      if (descriptor.ImplementationFactory is not null) {
        return descriptor.ImplementationFactory(currentScope);
      }
      if (descriptor.ImplementationInstance is not null) {
        return descriptor.ImplementationInstance;
      }
      
      if (descriptor.ImplementationType is null) return null;
      implementationType = SpecializeIfNeeded(serviceType, descriptor.ImplementationType);
    }
    
    try {
      return ResolveConstructorParameters(currentScope, implementationType);
    } catch (Exception ex) {
      throw new DependencyResolutionException($"Error resolving service '{descriptor.ServiceType}'", ex);
    }
  }
  private static Type SpecializeIfNeeded(Type serviceType, Type implementationType) {
    return implementationType.ContainsGenericParameters ? implementationType.MakeGenericType(serviceType.GenericTypeArguments) : implementationType;
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

      return serviceType == typeof(IServiceScopeFactory) ? _hybridServiceProvider : _hybridServiceProvider.GetService(serviceType, this);

    }

    /// <summary>
    /// Gets the service object of the specified type with the specified key.
    /// </summary>
    /// <param name="serviceType">The type of the service to get.</param>
    /// <param name="serviceKey">The key of the service to get.</param>
    /// <returns>The service object or null if not found.</returns>
    public object? GetKeyedService(Type serviceType, object? serviceKey) {
      return serviceKey is null ? GetService(serviceType) : _hybridServiceProvider.GetKeyedService(serviceType, serviceKey, this);

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

    internal object? ResolveService(Type serviceType, ServiceDescriptor descriptor) {
      if (_scopedInstances.TryGetValue(descriptor, out var instance)) {
        return instance;
      }

      var service = CreateServiceInstance(serviceType, descriptor, CompileTimeScope);
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