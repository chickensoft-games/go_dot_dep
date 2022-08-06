# GoDotDep
[![Chickensoft Badge][chickensoft-badge]][chickensoft-website] [![Discord](https://img.shields.io/badge/Chickensoft%20Discord-%237289DA.svg?style=flat&logo=discord&logoColor=white)][discord] ![line coverage][line-coverage] ![branch coverage][branch-coverage]

Node-based dependency injection for C# Godot games.

GoDotDep allows you to create nodes which declare dependencies on values provided by ancestor nodes in the scene tree. No more passing values all the way down the tree manually! GoDotDep is loosely inspired by [other popular dependency injection systems][provider] where trees are involved.

GoDotDep is designed to make injecting dependencies into child nodes easier by allowing nodes to provide values, cache the provider nodes which provide dependencies from dependent nodes, and access the dependency values provided.

 By "dependency", we simply mean any instance of an object a node might need to perform its job. Essentially, "dependencies" are just instances of custom classes which perform game or application logic.

> Are you on discord? If you're building games with Godot and C#, we'd love to see you in the [Chickensoft Discord server][discord]! Please don't hesitate to reach out for help!

## Installation

Find the latest version of [GoDotDep][go_dot_dep_nuget] on nuget.

In your `*.csproj`, add the following snippet in your `<ItemGroup>`, save, and run `dotnet restore`. Make sure to replace `*VERSION*` with the latest version.

```xml
<PackageReference Include="Chickensoft.GoDotDep" Version="*VERSION*" />
```

> GoDotDep is fully tested with [go_dot_test]! Be sure to check out the [Chickensoft] organization for other Godot C# goodies!

## Why have a dependency system?

In Godot, providing values to descendent nodes typically requires parent nodes to pass values to children nodes via method calls, following the ["call down, signal up"][call-down-signal-up] architecture rule. Unfortunately, passing dependencies down each layer can create undesirable tight couplings between parent and child, as the parent has to know details of the child.

If a distant descendant node of the parent also needs the same value, the parent's children have to pass it down until it reaches the correct descendent, too. Not only is it an awful lot of typing to create all those methods which just pass an object to a child node, it makes the code harder to follow since the dependency must be traced through multiple script files.

GoDotDep solves this problem by allowing nodes to implement either `IProvider<TValue>` to provide values to descendants, or `IDependent` to read values from ancestor nodes.

## Using Dependencies

To create a node which provides a value to all of its descendant nodes, you must implement the `IProvider<T>` interface.

`IProvider` requires a single `Get()` method that returns an instance of the object it provides.

```csharp
public class MySceneNode : IProvider<MyObject> {
  // If this object has to be created in _Ready(), we can use `null!` since we
  // know the value will be valid after _Ready is called. This is as close as we
  // can get to the `late` modifier in Dart or `lateinit` in Kotlin.
  private MyObject _object = null!;

  // IProvider<MyObject> requires us to implement a single method:
  MyObject IProvider<MyObject>.Get() => _object;

  public override void _Ready() {
    _object = new MyObject();
    
    // Notify any dependencies that the values provided are now available.
    this.Provided();
  }
}
```

Once all of the values are initialized, the provider node must call `this.Provided()` to inform any dependent nodes that the provided values are now available. Any dependent nodes already in existence in the subtree will have their `Loaded()` methods called, allowing them to use the now-available dependencies.

Since a node can implement multiple `IProvider<>` interfaces (for each type of value provided), providers should only call `this.Provided()` after all of the values they provide are initialized.

Dependent nodes that are added after the provider node has initialized their dependencies will have their `Loaded()` method called right away.

> `this.Provided()` is necessary because `_Ready()` is called on child nodes *before* parent nodes due to [Godot's tree order][godot-tree-order]. If you try to use a dependency in a dependent node's `_Ready()` method, there's no guarantee that it's been created, which results in null exception errors. Since it's often not possible to create dependencies until `_Ready()`, provider nodes are expected to invoke `this.Provided()` once all of their provided values are created.

Nodes can provide multiple values just as easily.

```csharp
public class MySceneNode : IProvider<MyObject>, IProvider<MyOtherObject> {
  private MyObject _object = null!;

  private MyOtherObject _otherObject = null!;

  MyObject IProvider<MyObject>.Get() => _object;
  MyOtherObject IProvider<MyOtherObject>.Get() => _otherObject;

  public override void _Ready() {
    _object = new MyObject(/* ... */);
    _otherObject = new MyOtherObject(/* ... */);

    // Notify any dependencies that the values provided are now available.
    this.Provided();
  }
}
```

To use dependencies, a node must implement `IDependent` and call `this.Depend()` at the end of the `_Ready()` method.

Dependent nodes declare dependencies by creating a property with the `[Dependency]` attribute and calling the node extension method `this.DependOn` with the type of value they are depending on.

```csharp
[Dependency]
private ObjectA _a => this.DependOn<ObjectA>();

[Dependency]
private ObjectB _b => this.DependOn<ObjectB>();
```

The `IDependent` interface requires you to implement a single void method, `Loaded()`, which is called once all the values the node depends on have been initialized by their providers. For `Loaded()` to be called, you must call `this.Depend()` in your dependent node's `_Ready()` method.

```csharp
public void Loaded() {
  // _a and _b are guaranteed to be non-null here.
  _a.DoSomething();
  _b.DoSomething();
}
```

> Internally, `this.Depend()` will look up all of the properties of your node which have a `[Dependency]` attribute and cache their providers for future access. If a provider hasn't initialized a dependency, hooks will be registered which call your dependent node's `Loaded()` method once all the dependencies are available. 

 In `Loaded()`, dependent nodes are guaranteed to be able to access their dependency values. Below is a complete example.

```csharp
public class DependentNode : Node, IDependent {
  // As long as there's a node which implements IProvider<MyObject> above us,
  // we will be able to access this object once `Loaded()` is called.
  [Dependency]
  private MyObject _object => this.DependOn<MyObject>();

  public override void _Ready() {
    // _object might actually be null here if the parent provider doesn't create
    // it in its constructor. Since many providers won't be creating 
    // dependencies until their _Ready() is invoked, which happens *after*
    // child node, we shouldn't reference dependencies in dependent nodes'
    // _Ready() methods.

    this.Depend();
  }

  public void Loaded() {
    // This method is called by the dependency system when all of the provided
    // values we depend on have been finalized by their providers!
    //
    // _object is guaranteed to be initialized here!
    _object.DoSomething();
  }
}
```

*Note*: If the dependency system can't find the correct provider in a dependent node's ancestors, it will search all of the autoloads for an autoload which implements the correct provider type. This allows you to "fallback" to global providers (should you want to).

## Dependency Caveats

Like all dependency injection systems, there are a few corner cases you should be aware of.

### Removing and Re-adding Nodes

If a node is removed from the tree and inserted somewhere else in the tree, it might try to use a cached version of the wrong provider. To prevent invalid
situations like this, you should clear the dependency cache and recreate it when a node re-enters the tree. This can be accomplished by simply calling `this.Depend()` again from the dependent node, which will call `Loaded()` again.

By placing provider nodes above all the possible parents of a node which depends on that value, you can ensure that a node will always be able to find the dependency it requests. Clever provider hierarchies will prevent most of these headaches.

### Dependency Deadlock

If you initialize dependencies in a complex (or slow way) by failing to call `this.Provided()` from your provider's `_Ready()` method, there is a risk of seriously slowing down (or deadlocking) the dependency resolution in the children. `Loaded()` isn't called on child nodes using `this.Depend()` until **all** of the dependencies they depend on from the ancestor nodes have been provided, so `Loaded()` will only be invoked when the slowest dependency has been marked provided via `this.Provided()` in the ancestor provider node.

To avoid this situation entirely, always initialize dependencies in your provider's `_Ready()` method and call `this.Provided()` immediately afterwards.

### Performance Considerations

What about performance? Dependency resolution runs in `O(n) * d` (worst case), where `n` is the depth of the dependent node in the scene tree and `d` is the number of dependencies required by your node. Once a node has found a provider for its dependency, further access is `O(1)` (instantaneous). GoDotDep only has to walk the ancestors once, checking to see if each ancestor matches any of the required dependencies.

If needed, you can create another provider lower in your scene tree that also depends on a value above it and "reflects" it to its descendants down below. Re-providing a value lower in the tree above a large number of dependents can drastically shorten the length of the tree that must be searched for dependent nodes to resolve their dependencies.

```csharp
public class ReProvider : Node, IDependent, IProvider<MyObject> {
  [Dependency]
  private MyObject _object => this.DependOn<MyObject>();

  MyObject IProvider<MyObject>.Get() => _object;

  public override void _Ready() => this.Depend();

  // Once the dependency is available to us, re-provide it to our
  // descendants so they don't have to search as far for it.
  // This optimization is probably only needed in really big scene trees.
  public void Loaded() => this.Provided();
}
```

<!-- Links -->

[chickensoft-badge]: https://chickensoft.games/images/chickensoft/chickensoft_badge.svg
[chickensoft-website]: https://chickensoft.games
[discord]: https://discord.gg/gSjaPgMmYW
[line-coverage]: https://raw.githubusercontent.com/chickensoft-games/go_dot_test/main/test/reports/line_coverage.svg
[branch-coverage]: https://raw.githubusercontent.com/chickensoft-games/go_dot_test/main/test/reports/branch_coverage.svg
[go_dot_test]: https://github.com/chickensoft-games/go_dot_test
[Chickensoft]: https://github.com/chickensoft-games
[go_dot_dep_nuget]: https://www.nuget.org/packages/Chickensoft.GoDotDep/
[provider]: https://pub.dev/packages/provider
[call-down-signal-up]: https://kidscancode.org/godot_recipes/basics/node_communication/
[godot-tree-order]: https://kidscancode.org/godot_recipes/basics/tree_ready_order/
