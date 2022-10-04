namespace GoDotDep {
  using System;
  using System.Runtime.CompilerServices;

  /// <summary>Base type for all provider nodes.</summary>
  public interface IProviderNode { }

  /// <summary>Represents a provider for a value of type
  /// <typeparamref name="TValue"/>.
  /// </summary>
  /// <typeparam name="TValue">Type of value to be provided. Should be a
  /// non-null reference type.</typeparam>
  public interface IProvider<TValue> : IProviderNode where TValue : class {
    /// <summary>
    /// The dependency value provided by this provider.
    /// </summary>
    /// <returns>An instance of a non-null reference type.</returns>
    TValue Get();
  }

  /// <summary>
  /// Default provider used internally to provide fallback values. Providing
  /// fallback values in `this.Depend()` from your dependent node enables your
  /// scene to be tested by itself without needing any providers in the tree
  /// above it.
  /// </summary>
  internal class DefaultProvider : IProviderNode {
    public Func<object> Get { get; init; }

    public DefaultProvider(Func<object> get) => Get = get;
  }

  /// <summary>
  /// Extension for <see cref="IProviderNode" /> that allows providers to call
  /// <see cref="Provided(IProviderNode)"/>.
  /// </summary>
  public static class IProviderExtension {
    // Use ConditionalWeakTable as a stateful mixin system. ConditionalWeakTable
    // allows us to add fields to objects, and it automagically removes entries
    // that have been garbage collected!
    // see See https://codecrafter.blogspot.com/2011/03/c-mixins-with-state.html

    private static readonly ConditionalWeakTable<IProviderNode, ProviderState>
      _providerStates = new();

    private static ProviderState GetState(IProviderNode node)
      => _providerStates.GetOrCreateValue(node);

    /// <summary>
    /// Announces to any dependencies listening that the provider has provided
    /// all of its values.
    /// <br />
    /// Providers should call this when all of their provided values have been
    /// initialized.
    /// <br />
    /// When all of a dependent node's providers have called
    /// <see cref="Provided(IProviderNode)" />, the dependent node's
    /// <see cref="IDependent.Loaded"/> method will be called.
    /// </summary>
    public static void Provided(this IProviderNode provider) {
      var state = GetState(provider);
      state.HasProvided = true;
      state.Announce(provider);
    }

    /// <summary>
    /// Checks to see if the provider has finished providing dependencies to
    /// nodes lower in the tree.
    /// </summary>
    /// <param name="provider">Receiver provider node.</param>
    /// <returns>True if the receiver has provided values.</returns>
    internal static bool HasProvided(this IProviderNode provider)
      => GetState(provider).HasProvided;

    /// <summary>
    /// Subscribes the given action to the receiver's
    /// <see cref="ProviderState.OnProvided"/> event.
    /// </summary>
    /// <param name="provider">Receiver provider node.</param>
    /// <param name="onProvided">Action to perform when the receiver has
    /// finished providing values.</param>
    internal static void Listen(
      this IProviderNode provider, Action<IProviderNode>? onProvided
    ) => GetState(provider).OnProvided += onProvided;

    /// <summary>
    /// Unsubscribes the given action from the receiver's
    /// <see cref="ProviderState.OnProvided"/> event.
    /// </summary>
    /// <param name="provider">Receiver provider node.</param>
    /// <param name="onProvided">Action to perform when the receiver has
    /// finished providing values.</param>
    internal static void StopListening(
      this IProviderNode provider, Action<IProviderNode>? onProvided
    ) => GetState(provider).OnProvided -= onProvided;
  }

  /// <summary>
  /// Provider state used internally when resolving dependencies.
  /// </summary>
  internal class ProviderState {
    /// <summary>True if the provider has provided all of its values.</summary>
    internal volatile bool HasProvided = false;

    /// <summary>
    /// Underlying event delegate used to inform dependent nodes that the
    /// provider has initialized all of the values it provides.
    /// </summary>
    internal event Action<IProviderNode>? OnProvided;

    /// <summary>
    /// Invoke the OnProvided event with the specified provider node.
    /// </summary>
    /// <param name="provider">Provider node which has finished initializing
    /// the values it provides.</param>
    internal void Announce(IProviderNode provider)
      => OnProvided?.Invoke(provider);
  }
}
