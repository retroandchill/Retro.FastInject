using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Retro.FastInject.Core;

namespace Retro.FastInject.Dynamic;

/// <summary>
/// A dynamic service provider that resolves services from an IServiceCollection.
/// </summary>
public class HybridServiceProvider : IServiceProvider, IKeyedServiceProvider, IServiceScopeFactory, IDisposable, IAsyncDisposable {
  private readonly Scope _rootScope;
  private readonly Dictionary<Type, List<ServiceDescriptor>> _descriptors = new();
  private readonly Dictionary<Type, Dictionary<object, List<ServiceDescriptor>>> _keyedDescriptors = new();
  private readonly Dictionary<ServiceDescriptor, object> _singletonInstances = new();
  private readonly List<DisposableWrapper> _disposables = [];
  private bool _disposed;

  /// <summary>
  /// Initializes a new instance of the <see cref="HybridServiceProvider"/> class.
  /// </summary>
  /// <param name="services">The service collection to use for resolving services.</param>
  public HybridServiceProvider(IServiceCollection services) {
    ArgumentNullException.ThrowIfNull(services);

    // Organize descriptors by service type and lifetime
    foreach (var descriptor in services) {

      if (descriptor.ServiceKey != null) {
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

    _rootScope = new Scope(this);
  }

  /// <summary>
  /// Gets the service object of the specified type.
  /// </summary>
  /// <param name="serviceType">The type of the service to get.</param>
  /// <returns>The service object or null if not found.</returns>
  public object? GetService(Type serviceType) {
    ObjectDisposedException.ThrowIf(_disposed, this);

    if (serviceType == typeof(IServiceProvider) || serviceType == typeof(IServiceScopeFactory)) {
      return this;
    }

    if (!_descriptors.TryGetValue(serviceType, out var descriptors) || descriptors.Count <= 0) return null;

    // Always use the last registered service when multiple registrations exist
    var descriptor = descriptors[^1];
    return ResolveService(descriptor, _rootScope);

  }

  /// <summary>
  /// Gets the service object of the specified type with the specified key.
  /// </summary>
  /// <param name="serviceType">The type of the service to get.</param>
  /// <param name="serviceKey">The key of the service to get.</param>
  /// <returns>The service object or null if not found.</returns>
  public object? GetKeyedService(Type serviceType, object? serviceKey) {
    ObjectDisposedException.ThrowIf(_disposed, this);

    if (serviceKey == null) {
      return GetService(serviceType);
    }

    if (!_keyedDescriptors.TryGetValue(serviceType, out var keyedServices) ||
        !keyedServices.TryGetValue(serviceKey, out var descriptors) ||
        descriptors.Count <= 0) return null;

    // Always use the last registered service when multiple registrations exist
    var descriptor = descriptors[^1];
    return ResolveService(descriptor, _rootScope);

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
    if (service == null) {
      throw new InvalidOperationException($"Service of type '{serviceType}' with key '{serviceKey}' cannot be resolved.");
    }

    return service;
  }

  /// <summary>
  /// Creates a new service scope.
  /// </summary>
  /// <returns>The service scope.</returns>
  public IServiceScope CreateScope() {
    ObjectDisposedException.ThrowIf(_disposed, this);

    return new Scope(this);
  }

  /// <summary>
  /// Disposes the service provider and all disposable services.
  /// </summary>
  public void Dispose() {
    if (_disposed) return;

    _disposed = true;

    _rootScope.Dispose();

    foreach (var instance in _singletonInstances.Values) {
      if (instance is IDisposable disposable) {
        disposable.Dispose();
      }
    }

    foreach (var disposable in _disposables) {
      disposable.Dispose();
    }

    _singletonInstances.Clear();
    _disposables.Clear();
  }

  /// <summary>
  /// Asynchronously disposes the service provider and all disposable services.
  /// </summary>
  public async ValueTask DisposeAsync() {
    if (_disposed) return;

    _disposed = true;

    await _rootScope.DisposeAsync();

    foreach (var instance in _singletonInstances.Values) {
      if (instance is IAsyncDisposable asyncDisposable) {
        await asyncDisposable.DisposeAsync();
      } else if (instance is IDisposable disposable) {
        disposable.Dispose();
      }
    }

    foreach (var disposable in _disposables) {
      await disposable.DisposeAsync();
    }

    _singletonInstances.Clear();
    _disposables.Clear();
  }

  private object? ResolveService(ServiceDescriptor descriptor, Scope currentScope) {
    if (descriptor.Lifetime == ServiceLifetime.Singleton) {
      if (_singletonInstances.TryGetValue(descriptor, out var instance)) {
        return instance;
      }

      var service = CreateServiceInstance(descriptor, currentScope);
      if (service != null) {
        _singletonInstances[descriptor] = service;
        RegisterForDisposal(service);
      }
      return service;
    } else if (descriptor.Lifetime == ServiceLifetime.Scoped) {
      return currentScope.ResolveService(descriptor);
    } else {
      // Transient
      var service = CreateServiceInstance(descriptor, currentScope);
      if (service != null) {
        currentScope.RegisterForDisposal(service);
      }
      return service;
    }
  }

  private object? CreateServiceInstance(ServiceDescriptor descriptor, Scope currentScope) {
    if (descriptor.ImplementationInstance != null) {
      return descriptor.ImplementationInstance;
    }

    if (descriptor.ImplementationFactory != null) {
      return descriptor.ImplementationFactory(currentScope);
    }

    if (descriptor.ImplementationType is null) return null;

    try {
      // Find constructor with the most parameters that we can resolve
      var constructors = descriptor.ImplementationType.GetConstructors()
          .OrderByDescending(c => c.GetParameters().Length)
          .ToList();

      foreach (var constructor in constructors) {
        var parameters = constructor.GetParameters();
        var parameterInstances = new object?[parameters.Length];
        var canResolveAll = true;

        for (int i = 0; i < parameters.Length; i++) {
          var parameter = parameters[i];

          // Check if the parameter is a keyed service
          var keyedServiceAttribute = parameter.GetCustomAttributes(true)
              .FirstOrDefault(a => a.GetType().Name == "FromKeyedServicesAttribute");

          object? parameterInstance;
          if (keyedServiceAttribute != null) {
            // Extract the key value from the attribute
            var key = keyedServiceAttribute.GetType().GetProperty("Key")?.GetValue(keyedServiceAttribute);
            parameterInstance = currentScope.GetKeyedService(parameter.ParameterType, key);
          } else {
            parameterInstance = currentScope.GetService(parameter.ParameterType);
          }

          if (parameterInstance == null && !parameter.IsOptional) {
            canResolveAll = false;
            break;
          }

          parameterInstances[i] = parameterInstance;
        }

        if (canResolveAll) {
          return constructor.Invoke(parameterInstances);
        }
      }

      // If no constructor works, try to create with default constructor
      return Activator.CreateInstance(descriptor.ImplementationType);
    } catch (Exception ex) {
      throw new InvalidOperationException($"Error resolving service '{descriptor.ServiceType}'", ex);
    }
  }

  private void RegisterForDisposal(object instance) {
    switch (instance) {
      case IDisposable disposable:
        _disposables.Add(new DisposableWrapper(disposable, instance as IAsyncDisposable));
        break;
      case IAsyncDisposable asyncDisposable:
        _disposables.Add(new DisposableWrapper(null, asyncDisposable));
        break;
    }
  }

  /// <summary>
  /// A scope that provides services from a service provider.
  /// </summary>
  public class Scope : IServiceProvider, IKeyedServiceProvider, IServiceScope, IDisposable, IAsyncDisposable {
    private readonly HybridServiceProvider _hybridServiceProvider;
    private readonly Dictionary<ServiceDescriptor, object> _scopedInstances = new();
    private readonly List<DisposableWrapper> _disposables = [];
    private bool _disposed;

    /// <summary>
    /// Gets the service provider for this scope.
    /// </summary>
    public IServiceProvider ServiceProvider => this;

    /// <summary>
    /// Initializes a new instance of the <see cref="Scope"/> class.
    /// </summary>
    /// <param name="hybridServiceProvider">The service provider.</param>
    public Scope(HybridServiceProvider hybridServiceProvider) {
      _hybridServiceProvider = hybridServiceProvider;
    }

    /// <summary>
    /// Gets the service object of the specified type.
    /// </summary>
    /// <param name="serviceType">The type of the service to get.</param>
    /// <returns>The service object or null if not found.</returns>
    public object? GetService(Type serviceType) {
      ObjectDisposedException.ThrowIf(_disposed, this);

      if (serviceType == typeof(IServiceProvider)) {
        return this;
      }

      if (serviceType == typeof(IServiceScopeFactory)) {
        return _hybridServiceProvider;
      }

      if (!_hybridServiceProvider._descriptors.TryGetValue(serviceType, out var descriptors) || descriptors.Count <= 0) return null;

      // Always use the last registered service when multiple registrations exist
      var descriptor = descriptors[^1];
      return _hybridServiceProvider.ResolveService(descriptor, this);

    }

    /// <summary>
    /// Gets the service object of the specified type with the specified key.
    /// </summary>
    /// <param name="serviceType">The type of the service to get.</param>
    /// <param name="serviceKey">The key of the service to get.</param>
    /// <returns>The service object or null if not found.</returns>
    public object? GetKeyedService(Type serviceType, object? serviceKey) {
      
      ObjectDisposedException.ThrowIf(_disposed, this);

      if (serviceKey == null) {
        return GetService(serviceType);
      }

      if (!_hybridServiceProvider._keyedDescriptors.TryGetValue(serviceType, out var keyedServices) ||
          !keyedServices.TryGetValue(serviceKey, out var descriptors) ||
          descriptors.Count <= 0) return null;

      // Always use the last registered service when multiple registrations exist
      var descriptor = descriptors[^1];
      return _hybridServiceProvider.ResolveService(descriptor, this);

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
      if (service == null) {
        throw new InvalidOperationException($"Service of type '{serviceType}' with key '{serviceKey}' cannot be resolved.");
      }

      return service;
    }

    /// <summary>
    /// Disposes the scope and all disposable services.
    /// </summary>
    public void Dispose() {
      if (_disposed) return;

      _disposed = true;

      foreach (var instance in _scopedInstances.Values) {
        if (instance is IDisposable disposable) {
          disposable.Dispose();
        }
      }

      foreach (var disposable in _disposables) {
        disposable.Dispose();
      }

      _scopedInstances.Clear();
      _disposables.Clear();
    }

    /// <summary>
    /// Asynchronously disposes the scope and all disposable services.
    /// </summary>
    public async ValueTask DisposeAsync() {
      if (_disposed) return;

      _disposed = true;

      foreach (var instance in _scopedInstances.Values) {
        switch (instance) {
          case IAsyncDisposable asyncDisposable:
            await asyncDisposable.DisposeAsync();
            break;
          case IDisposable disposable:
            disposable.Dispose();
            break;
        }
      }

      foreach (var disposable in _disposables) {
        await disposable.DisposeAsync();
      }

      _scopedInstances.Clear();
      _disposables.Clear();
    }

    internal object? ResolveService(ServiceDescriptor descriptor) {
      if (_scopedInstances.TryGetValue(descriptor, out var instance)) {
        return instance;
      }

      var service = _hybridServiceProvider.CreateServiceInstance(descriptor, this);
      if (service != null) {
        _scopedInstances[descriptor] = service;
        RegisterForDisposal(service);
      }
      return service;
    }

    internal void RegisterForDisposal(object instance) {
      switch (instance) {
        case IDisposable disposable:
          _disposables.Add(new DisposableWrapper(disposable, instance as IAsyncDisposable));
          break;
        case IAsyncDisposable asyncDisposable:
          _disposables.Add(new DisposableWrapper(null, asyncDisposable));
          break;
      }
    }
  }
}