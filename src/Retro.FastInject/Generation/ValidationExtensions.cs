using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Retro.FastInject.Comparers;
using Retro.FastInject.Model.Detection;
using Retro.FastInject.Model.Manifest;

namespace Retro.FastInject.Generation;

/// <summary>
/// Provides extension methods to validate the dependency graph in a service manifest.
/// Useful for ensuring proper dependency management and detecting issues such as circular dependencies
/// within the dependency injection process.
/// </summary>
public static class ValidationExtensions {
  
  /// <summary>
  /// Validates the entire dependency graph for circular dependencies.
  /// This should be called after all constructor dependencies have been resolved.
  /// </summary>
  /// <param name="serviceManifest">The service manifest to validate.</param>
  /// <exception cref="InvalidOperationException">Thrown when a circular dependency is detected.</exception>
  public static void ValidateDependencyGraph(this ServiceManifest serviceManifest) {
    var visited = new HashSet<ITypeSymbol>(TypeSymbolEqualityComparer.Instance);
    var path = new Stack<ITypeSymbol>();
    var onPath = new HashSet<ITypeSymbol>(TypeSymbolEqualityComparer.Instance);
    
    foreach (var serviceType in serviceManifest.GetAllServices().Select(x => x.Type)) {
      if (visited.Contains(serviceType)) continue;

      if (serviceManifest.DetectCycle(serviceType, visited, path, onPath, out var cycle)) {
        throw new InvalidOperationException(
            $"Detected circular dependency: {string.Join(" → ", cycle.Select(t => t.ToDisplayString()))}");
      }
    }
  }
  
  private static bool DetectCycle(this ServiceManifest serviceManifest, ITypeSymbol type, HashSet<ITypeSymbol> visited, Stack<ITypeSymbol> path, 
                          HashSet<ITypeSymbol> onPath, [NotNullWhen(true)] out List<ITypeSymbol>? cycle) {
    cycle = null;
    
    
    if (onPath.Contains(type)) {
      return ExtractCycleFromPath(type, path, out cycle);
    }
    
    if (!visited.Add(type)) {
      return false; // Already visited and no cycle found
    }

    onPath.Add(type);
    path.Push(type);
    
    // If we have a constructor resolution for this type, check its dependencies
    if (serviceManifest.TryGetConstructorResolution(type, out var resolution) 
        && serviceManifest.CheckServiceCycle(visited, path, onPath, ref cycle, resolution)) return true;

    // Done with this node
    path.Pop();
    onPath.Remove(type);
    return false;
  }
  private static bool CheckServiceCycle(this ServiceManifest serviceManifest, HashSet<ITypeSymbol> visited, 
                                 Stack<ITypeSymbol> path, 
                                 HashSet<ITypeSymbol> onPath, 
                                 [NotNullWhen(true)] ref List<ITypeSymbol>? cycle, 
                                 ConstructorResolution resolution) {
    foreach (var serviceRegistration in resolution.Parameters
                 .Select(param => new {
                     param,
                     isNullable = param.Parameter.Type.NullableAnnotation == NullableAnnotation.Annotated
                 })
                 .Where(t => !t.isNullable && t.param.DefaultValue == null)
                 .Select(t => t.param.SelectedService)) {
      // Check the selected service type if available
      if (serviceRegistration is null) continue;

      var serviceType = serviceRegistration.ResolvedType;
      if (serviceManifest.DetectCycle(serviceType, visited, path, onPath, out cycle)) {
        return true;
      }
    }
    return false;
  }
  private static bool ExtractCycleFromPath(ITypeSymbol type, Stack<ITypeSymbol> path, out List<ITypeSymbol> cycle) {
    // We found a cycle
    cycle = [];
    var cycleStarted = false;
      
    // Extract the cycle from the path
    foreach (var node in path.Reverse()) {
      if (TypeSymbolEqualityComparer.Instance.Equals(node, type)) {
        cycleStarted = true;
      }
        
      if (cycleStarted) {
        cycle.Add(node);
      }
    }
      
    cycle.Add(type); // Complete the cycle
    return true;
  }
  
}