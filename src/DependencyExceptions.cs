namespace GoDotDep;
using System;
using System.Collections.Generic;

/// <summary>
/// Exception thrown when a provider node cannot be found
/// in any of the dependent node's ancestors while resolving dependencies.
/// </summary>
public class ProviderNotFoundException : InvalidOperationException {
  /// <summary>Creates a new provider not found exception.</summary>
  /// <param name="providerTypes">Types of providers that were not
  /// found.</param>
  public ProviderNotFoundException(HashSet<Type> providerTypes) : base(
    $"No provider found for the following types: " +
    $"{string.Join(", ", providerTypes)}"
  ) { }
}

public class DependentRequestedSupertypeException : InvalidOperationException {
  public DependentRequestedSupertypeException(
    Type requestedType, Type providedType
  ) : base(
    $"The requested dependency type `{requestedType.Name}` is a supertype " +
    $"of the provided type `{providedType.Name}`. Please request the " +
    $"provided type `{providedType.Name}` instead."
  ) { }
}

/// <summary>
/// Exception thrown if another class implements <see cref="IDependent"/> and
/// tries to call
/// <see cref="IDependentExtension.Depend(IDependent, Dictionary{Type, Func{object}}?)"/>.
/// </summary>
public class DependentNotAGodotNodeException : InvalidOperationException {
  /// <summary>Creates a new exception.</summary>
  public DependentNotAGodotNodeException() : base(
    "Only Godot nodes should implement IDependent."
  ) { }
}

/// <summary>
/// Exception thrown when an <see cref="IDependent"/> node calls
/// <see cref="IDependentExtension.Depend(IDependent, Dictionary{Type, Func{object}}?)"/>
/// before a provider has provided the value for a dependency.
/// </summary>
public class ProviderNotReadyException : InvalidOperationException {
  /// <summary>Creates a new provider not ready exception.</summary>
  /// <param name="dependencyType">Dependency that was accessed before the
  /// provider was ready.</param>
  public ProviderNotReadyException(Type dependencyType) : base(
    $"A value of type `{dependencyType}` was accessed before the provider " +
    "was ready. Please call `this.Depend()` in your dependent node," +
    "wait until `Loaded()` is called before using any dependencies, " +
    "and call `this.Provided()` from your provider when dependencies are " +
    "initialized."
  ) { }
}
