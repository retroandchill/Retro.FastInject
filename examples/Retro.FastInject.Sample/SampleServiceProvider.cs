using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Retro.FastInject.Core;
using Retro.FastInject.Sample.Services;

namespace Retro.FastInject.Sample;

public sealed class SampleServiceProvider : IServiceProvider, 
                                            IServiceProvider<ISingletonService>,
                                            IServiceProvider<SingletonService>,
                                            IServiceProvider<IScopedService>,
                                            IServiceProvider<ScopedService>,
                                            IServiceProvider<ITransientService>,
                                            IServiceProvider<TransientService>,
                                            IKeyedServiceProvider, 
                                            IServiceScopeFactory, 
                                            IDisposable, 
                                            IAsyncDisposable {

  private Scope? _rootScope;
  private SingletonService? _singletonService;
  
  private Scope GetRootScope() {
      return LazyInitializer.EnsureInitialized(ref _rootScope, () => new Scope(this));
  }

  public object? GetService(Type serviceType) {
    if (serviceType == typeof(IServiceProvider) || serviceType == typeof(IServiceScopeFactory)) {
      return this;
    }

    if (serviceType == typeof(ISingletonService)) {
      return ((IServiceProvider<ISingletonService>) this).GetService();
    }
    
    if (serviceType == typeof(SingletonService)) {
      return ((IServiceProvider<SingletonService>) this).GetService();
    }
    
    if (serviceType == typeof(ScopedService)) {
      return ((IServiceProvider<ScopedService>) this).GetService();
    }
    
    if (serviceType == typeof(ITransientService)) {
      return ((IServiceProvider<ITransientService>) this).GetService();
    }
    
    if (serviceType == typeof(TransientService)) {
      return ((IServiceProvider<TransientService>) this).GetService();
    }
    
    return null;
  }

  public object? GetKeyedService(Type serviceType, object? serviceKey) {
    return null;
  }

  public object GetRequiredKeyedService(Type serviceType, object? serviceKey) {
    throw new KeyNotFoundException($"Could not find service of type {serviceType} with key {serviceKey}");
  }

  public IServiceScope CreateScope() {
    throw new NotImplementedException();
  }

  public void Dispose() {
    _rootScope?.Dispose();
  }

  public async ValueTask DisposeAsync() {
    await TryDispose(_rootScope);
  }

  private ValueTask TryDispose(IAsyncDisposable? disposable) {
    return disposable?.DisposeAsync() ?? default;

  }

  ISingletonService IServiceProvider<ISingletonService>.GetService() {
    return ((IServiceProvider<SingletonService>) this).GetService();
  }

  SingletonService IServiceProvider<SingletonService>.GetService() {
    return LazyInitializer.EnsureInitialized(ref _singletonService, () => new SingletonService());
  }

  IScopedService IServiceProvider<IScopedService>.GetService() {
    return ((IServiceProvider<IScopedService>) this).GetService();
  }

  ScopedService IServiceProvider<ScopedService>.GetService() {
    return ((IServiceProvider<ScopedService>) GetRootScope()).GetService();
  }

  ITransientService IServiceProvider<ITransientService>.GetService() {
    return ((IServiceProvider<TransientService>) this).GetService();
  }

  TransientService IServiceProvider<TransientService>.GetService() {
    return new TransientService();
  }

  public sealed class Scope : IServiceProvider, 
                       IKeyedServiceProvider,
                       IServiceScope,
                       IServiceProvider<ISingletonService>,
                       IServiceProvider<SingletonService>,
                       IServiceProvider<IScopedService>,
                       IServiceProvider<ScopedService>,
                       IServiceProvider<ITransientService>,
                       IServiceProvider<TransientService>, 
                       IDisposable, 
                       IAsyncDisposable {
    
    private readonly SampleServiceProvider _root;
    
    private ScopedService? _scopedService;

    public IServiceProvider ServiceProvider => this;
    
    public Scope(SampleServiceProvider root) {
      _root = root;
    }

    public object? GetService(Type serviceType) {
      throw new NotImplementedException();
    }

    public object? GetKeyedService(Type serviceType, object? serviceKey) {
      throw new NotImplementedException();
    }

    public object GetRequiredKeyedService(Type serviceType, object? serviceKey) {
      throw new NotImplementedException();
    }

    ISingletonService IServiceProvider<ISingletonService>.GetService() {
      return ((IServiceProvider<ISingletonService>) _root).GetService();
    }

    SingletonService IServiceProvider<SingletonService>.GetService() {
      return ((IServiceProvider<SingletonService>) _root).GetService();
    }

    IScopedService IServiceProvider<IScopedService>.GetService() {
      return ((IServiceProvider<ScopedService>) this).GetService();
    }

    ScopedService IServiceProvider<ScopedService>.GetService() {
      var singletonService = ((IServiceProvider<ISingletonService>) this).GetService();
      var transientService = ((IServiceProvider<ITransientService>) this).GetService();
      return LazyInitializer.EnsureInitialized(ref _scopedService, () => new ScopedService(singletonService, transientService));
    }

    ITransientService IServiceProvider<ITransientService>.GetService() {
      return ((IServiceProvider<ITransientService>) _root).GetService();
    }

    TransientService IServiceProvider<TransientService>.GetService() {
      return ((IServiceProvider<TransientService>) _root).GetService();
    }

    public void Dispose() {
      // No need to dispose anything here
    }

    public async ValueTask DisposeAsync() {
      // No disposal need
    }
  }
}