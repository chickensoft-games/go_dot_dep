namespace GoDotDep;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;

/// <summary>
/// All nodes which use GoDotDep to depend on values must implement this
/// interface.
/// </summary>
public interface IDependent {
  /// <summary>
  /// Method that is called when all of the node's dependencies are available
  /// from the providers that it depends on.
  ///
  /// For this method to be called, you must call
  /// <see cref="IDependentExtension.Depend(IDependent, Dictionary{Type, Func{object}}?)"/>
  /// from your dependent node's _Ready method.
  /// </summary>
  public void Loaded();
}


/// <summary>
/// Extension class which supplies the
/// <see cref="Depend(IDependent, Dictionary{Type, Func{object}}?)"/>
/// method to Godot nodes that implement <see cref="IDependent"/>.
/// </summary>
public static class IDependentExtension {
  // Essentially a typedef for a Dictionary that maps Types to object getters.
  private class Dependencies : Dictionary<Type, IProviderNode> { }

  // Use ConditionalWeakTable as a stateful mixin system. ConditionalWeakTable
  // allows us to add fields to objects, and it automagically removes entries
  // that have been garbage collected!
  // see See https://codecrafter.blogspot.com/2011/03/c-mixins-with-state.html

  private static readonly ConditionalWeakTable<IDependent, Dependencies>
    _deps = new();

  private static void SetDeps(this IDependent dependent, Dependencies deps) =>
    _deps.AddOrUpdate(dependent, deps);

  /// <summary>
  /// Returns the dependencies of the receiver node.
  /// </summary>
  /// <param name="dependent">Receiver node.</param>
  /// <returns>Dictionary of dependencies, keyed by type.</returns>
  private static Dependencies GetDeps(this IDependent dependent) =>
    _deps.GetOrCreateValue(dependent);

  /// <summary>
  /// Depends on a non-null reference value of type
  /// <typeparamref name="TValue"/>. This should be the return value for each
  /// dependency getter in your node.
  /// <br />
  /// If a provider node that implements <see cref="IProvider{TValue}"/> is
  /// an ancestor of the receiver node, this will return the value provided by
  /// that provider. Otherwise, a <see cref="ProviderNotFoundException"/> will
  /// be thrown.
  /// </summary>
  /// <throws name="ProviderNotFoundException" />
  /// <throws name="ProviderNotReadyException" />
  /// <param name="dependent">The receiver node that implements
  /// <see cref="IDependent"/>.</param>
  /// <typeparam name="TValue">Type of value to be depended upon.</typeparam>
  /// <returns>The dependency value provided by an ancestor provider that
  /// implements <see cref="IProvider{TValue}"/>.</returns>
  public static TValue DependOn<TValue>(this IDependent dependent)
    where TValue : class {
    var dependencyTable = GetDeps(dependent);
    if (dependencyTable.TryGetValue(typeof(TValue), out var providerNode)) {
      if (providerNode is DefaultProvider defaultProvider) {
        return (TValue)defaultProvider.Get();
      }
      if (!providerNode.HasProvided()) {
        throw new ProviderNotReadyException(typeof(TValue));
      }
      return (providerNode as IProvider<TValue>)!.Get();
    }
    else {
      throw new ProviderNotFoundException(
        providerTypes: new HashSet<Type>() { typeof(TValue) }
      );
    }
  }

  /// <summary>
  /// Begins the dependency resolution process. For each field marked with the
  /// <see cref="DependencyAttribute"/>, the node's ancestors will be searched
  /// for a corresponding node that implements
  /// <see cref="IProvider{TValue}"/>. When all providers are found, GoDotDep
  /// will subscribe to the providers and subscribe to any providers whose
  /// values are not yet ready. When all values are ready,
  /// <see cref="IDependent.Loaded"/> will be invoked on the dependent node
  /// that called this method.
  /// </summary>
  /// <param name="dependent">Dependent node which invoked the method.</param>
  /// <param name="defaultValues">Dictionary which maps types to functions
  /// that return an instance of that type. Used as fallback values when
  /// no provider can be found for dependencies of that type.<br />
  /// Providing a dictionary of default values can enable your scene to be
  /// run by itself from the editor with test data.</param>
  public static void Depend(
    this IDependent dependent,
    Dictionary<Type, Func<object>>? defaultValues = null
  ) {
    // Clear any existing dependency cache.
    dependent.SetDeps(new());

    if (dependent is not Node) {
      throw new DependentNotAGodotNodeException();
    }

    var node = (Node)dependent;
    var myType = dependent.GetType();

    // Get all properties tagged with the [Dependency] attribute.
    // We have to search all of our superclasses that might implement
    // IDependent.
    var typesNeeded = new HashSet<Type>();
    var currentType = myType;
    while (currentType != null) {
      if (currentType.GetInterface(nameof(IDependent)) != null) {
        var types = currentType.GetProperties(
          BindingFlags.Instance |
          BindingFlags.Public |
          BindingFlags.NonPublic
        ).Where(
          propertyInfo =>
            propertyInfo.GetCustomAttribute<DependencyAttribute>() != null
        ).Select(prop => prop.PropertyType);
        typesNeeded.UnionWith(types);
      }
      currentType = currentType.BaseType;
    }

    if (typesNeeded.Count < 1) {
      // There's no dependencies! Nothing to do :)
      return;
    }

    // Search the scene tree ancestors, beginning with our parent until we
    // find provider node(s) for every dependency we need.

    var classType = typeof(IDependentExtension);
    var getProviderMethod = classType.GetMethod(
      nameof(IDependentExtension.IsProvider),
      BindingFlags.Static | BindingFlags.NonPublic
    );

    // Create a closure which can be used to track how many dependencies still
    // need to be provided before the dependent node is allowed to use them.
    var numTypesToDependOn = typesNeeded.Count;
    void onDependencyLoaded(IProviderNode provider) {
      numTypesToDependOn--;
      if (numTypesToDependOn == 0) {
        dependent.Loaded();
      }
      if (provider is not DefaultProvider) {
        provider.StopListening(onDependencyLoaded);
      }
    }

    var ancestor = node.GetParent();

    var dependencyTable = GetDeps(dependent);

    while (ancestor != null) {
      var typesFound = new HashSet<Type>();
      foreach (var type in typesNeeded) {
        if (ancestor.GetType().IsProvider(type)) {
          // The current ancestor node is a provider for this dependency type.
          var provider = (IProviderNode)ancestor;

          // Add dependency to our internal dependency table. The internal
          // dependency table is made possible by the stateful mixin approach
          // which uses ConditionalWeakTable (see above).
          dependencyTable[type] = provider;

          // Add our type to the list of types found. We'll remove those from
          // the set of types needed once we're done iterating.
          typesFound.Add(type);

          if (provider.HasProvided()) {
            // Provider has already provided, so we indicate that this
            // dependency is already available.
            onDependencyLoaded(provider);
          }
          else {
            // Provider hasn't provided the value, so we'll wait until it has
            // before we call `Loaded()` on ourselves.
            provider.Listen(onDependencyLoaded);
          }

        }
      }

      // Remove any types we just found from the set of types to search for.
      typesNeeded.ExceptWith(typesFound);

      // No labeled break in C#, so we use goto.
      if (typesNeeded.Count == 0) { goto finishSearch; }

      // Keep looking for providers.
      ancestor = ancestor.GetParent();
    }

    finishSearch:
    if (defaultValues != null) {
      // Create default providers to provide any given fallback values.
      var typesFound = new HashSet<Type>();
      foreach (var type in typesNeeded) {
        if (defaultValues.ContainsKey(type)) {
          var provider = new DefaultProvider(defaultValues[type]);
          dependencyTable[type] = provider;
          onDependencyLoaded(provider);
          typesFound.Add(type);
        }
      }
      typesNeeded.ExceptWith(typesFound);
    }

    if (typesNeeded.Count != 0) {
      throw new ProviderNotFoundException(providerTypes: typesNeeded);
    }
  }

  // This method determines if a the receiver type implements
  // IProvider<genericType>. There's a crude performance test that measures
  // the speed of these reflection metadata methods in in
  // test/IsProviderPerformanceTest.cs. In general, this should be plenty
  // fast for most every use case. Since providers are cached once found,
  // performance could only be an issue when a node is first added to the scene.
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  internal static bool IsProvider(this Type type, Type genericType) {
    if (type.IsInterface && type.IsGenericType &&
        type.GetGenericTypeDefinition() == typeof(IProvider<>)) {
      return type.GetGenericArguments()[0] == genericType;
    }
    foreach (var i in type.GetInterfaces()) {
      if (
        i.IsGenericType &&
        i.GetGenericTypeDefinition() == typeof(IProvider<>)
      ) {
        var providedType = i.GetGenericArguments()[0];
        // as a dependent, it's easy to accidentally request a supertype of
        // a provided type, such as `GameState` instead of the provided
        // `IGameState`. throwing an error in this scenario prevents
        // type collision in the provider hierarchy and informs the developer
        // if they make this mistake, saving them valuable debugging time.
        if (
          providedType != genericType &&
          providedType.IsAssignableFrom(genericType)
        ) {
          throw new DependentRequestedSupertypeException(
            requestedType: genericType,
            providedType: providedType
          );
        }
        if (providedType == genericType) { return true; }
      }
    }
    return false;
  }
}
