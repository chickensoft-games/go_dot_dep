namespace GoDotDepTests;
using System;
using Godot;
using GoDotDep;
using GoDotTest;
using Shouldly;

public class ITestDummyValueA { }

public class TestDummyValueA : ITestDummyValueA { }
public class TestDummyValueB { }
public record TestDummyValueC {
  public string Name { get; init; } = "Default value";
}

public partial class TestProviderOneNode : Node, IProvider<TestDummyValueA> {
  private readonly TestDummyValueA _value;

  public TestProviderOneNode(TestDummyValueA value) => _value = value;

  public TestDummyValueA Get() => _value;

  public override void _Ready() => this.Provided();
}

public partial class TestProviderSubtype : Node, IProvider<ITestDummyValueA> {
  private readonly TestDummyValueA _value;

  public TestProviderSubtype(TestDummyValueA value) => _value = value;

  public ITestDummyValueA Get() => _value;

  public override void _Ready() => this.Provided();
}

public partial class TestProviderTwoNode : Node, IProvider<TestDummyValueB> {
  private readonly TestDummyValueB _value;

  public TestProviderTwoNode(TestDummyValueB value) => _value = value;

  public TestDummyValueB Get() => _value;

  public override void _Ready() => this.Provided();
}

public partial class TestDependentNode : Node, IDependent {
  private Action<TestDependentNode> _loadedCallback { get; init; }

  public TestDependentNode(
    Action<TestDependentNode> loadedCallback
    ) => _loadedCallback = loadedCallback;

  [Dependency]
  public TestDummyValueA ValueA => this.DependOn<TestDummyValueA>();

  [Dependency]
  public TestDummyValueB ValueB => this.DependOn<TestDummyValueB>();

  // Ask to load dependencies on ready. This should result in Loaded() being
  // called after the provider's own _Ready() methods are called after us
  // (since parent _Ready invocations happen after children in Godot's tree
  // order).
  public override void _Ready() => this.Depend();

  // Create a side effect we can test.
  public void Loaded() => _loadedCallback(this);
}

public partial class TestDependentNodeOneValue : Node, IDependent {
  [Dependency]
  public TestDummyValueA ValueA => this.DependOn<TestDummyValueA>();

  public override void _Ready() => this.Depend();

  public void Loaded() { }
}

public partial class TestDependentNodeWithoutDepend : Node, IDependent {
  [Dependency]
  public TestDummyValueA ValueA => this.DependOn<TestDummyValueA>();

  public void Loaded() { }
}

public partial class TestProviderNodeWithoutProvided : Node, IProvider<TestDummyValueA> {
  public TestDummyValueA Get() => new();

  // This one never calls this.Provided()!
}

public partial class TestDependentWithDefaultValue : Node, IDependent {
  [Dependency]
  public TestDummyValueC ValueA => this.DependOn<TestDummyValueC>();

  private Action _loadedCallback { get; init; }

  public TestDependentWithDefaultValue(
    Action loadedCallback
    ) => _loadedCallback = loadedCallback;

  public override void _Ready() => this.Depend(new() {
    [typeof(TestDummyValueC)] =
      () => new TestDummyValueC() { Name = nameof(TestDependentWithDefaultValue) }
  });

  public void Loaded() => _loadedCallback();
}

public partial class TestDependentNotANode : IDependent {
  // This one doesn't inherit from Godot.Node!

  public void Ready() => this.Depend();
  public void Loaded() { }
}

public partial class TestDependentWithNoDependencies : Node, IDependent {
  public override void _Ready() => this.Depend();
  public void Loaded() { }
}

//
// Begin actual tests...
//

public class DependencyTest : TestClass {
  public DependencyTest(Node testScene) : base(testScene) { }

  [Test]
  public void ThrowsProviderNotFoundExceptionWhenProviderNotFound() {
    var dependent = new TestDependentNodeOneValue();
    Should.Throw<ProviderNotFoundException>(
      () => dependent._Ready()
    );
  }

  [Test]
  public void DependentIsLoadedWithValuesWhenProvidersProvidesValues() {
    // Test that Loaded() gets called when all dependencies are provided.
    // Make sure the dependencies are what we expect them to be.

    var loadedCalled = false;
    var value1 = new TestDummyValueA();
    var value2 = new TestDummyValueB();

    var dependent = new TestDependentNode(
      loadedCallback: (node) => {
        loadedCalled = true;
        node.ValueA.ShouldBe(value1);
        node.ValueB.ShouldBe(value2);
      }
    );

    var providerOne = new TestProviderOneNode(value: value1);
    var providerTwo = new TestProviderTwoNode(value: value2);

    providerOne.AddChild(providerTwo);
    providerTwo.AddChild(dependent);

    dependent._Ready();
    loadedCalled.ShouldBe(false);
    providerOne._Ready();
    loadedCalled.ShouldBe(false);
    providerTwo._Ready();
    loadedCalled.ShouldBe(true);
  }

  [Test]
  public void DependOnThrowsProviderNotFoundException() {
    var dependent = new TestDependentNodeWithoutDepend();
    dependent._Ready();
    Should.Throw<ProviderNotFoundException>(
      () => dependent.ValueA
    );
  }

  [Test]
  public void DependOnThrowsProviderNotReadyException() {
    var value2 = new TestDummyValueB();
    var providerOne = new TestProviderNodeWithoutProvided();
    var providerTwo = new TestProviderTwoNode(value: value2);
    var dependent = new TestDependentNode(loadedCallback: (node) => { });
    providerOne.AddChild(providerTwo);
    providerTwo.AddChild(dependent);
    providerOne._Ready();
    providerTwo._Ready();
    dependent._Ready();
    Should.Throw<ProviderNotReadyException>(
      () => dependent.ValueA
    );
  }

  [Test]
  public void DependThrowsDependentNotAGodotNodeException() {
    var dependent = new TestDependentNotANode();
    Should.Throw<DependentNotAGodotNodeException>(
      () => dependent.Ready()
    );
  }

  [Test]
  public void DependDoesNothingWhenNoDependencies() {
    var dependent = new TestDependentWithNoDependencies();
    Should.NotThrow(() => dependent._Ready());
  }

  [Test]
  public void DependShortCircuitsProviderSearchWhenAllProvidersFound() {
    var value1 = new TestDummyValueA();
    var value2 = new TestDummyValueB();
    var providerOne = new TestProviderOneNode(value: value1);
    var providerTwo = new TestProviderTwoNode(value: value2);

    var dependent = new TestDependentNodeOneValue();

    providerTwo.AddChild(providerOne);
    providerOne.AddChild(dependent);

    // Depend should exit the ancestor search before it reaches provider two
    // (which is above provider one in this test). If this test succeeds,
    // coverage will show that the search is short circuited.

    Should.NotThrow(() => dependent._Ready());
  }

  [Test]
  public void DependentLoadedCalledImmediatelyWhenProvidersAlreadyProvided() {
    var value1 = new TestDummyValueA();
    var value2 = new TestDummyValueB();
    var providerOne = new TestProviderOneNode(value: value1);
    var providerTwo = new TestProviderTwoNode(value: value2);

    var loadedCalled = false;

    var dependent = new TestDependentNode(
      loadedCallback: (node) => loadedCalled = true
    );

    providerOne.AddChild(providerTwo);

    providerOne._Ready();
    providerTwo._Ready();

    loadedCalled.ShouldBeFalse();
    providerTwo.AddChild(dependent);
    dependent._Ready();
    loadedCalled.ShouldBeTrue();
  }

  [Test]
  public void DependCreatesProvidersForDefaultValues() {
    var loadedCalled = false;
    var dependent = new TestDependentWithDefaultValue(
      loadedCallback: () => loadedCalled = true
    );
    dependent._Ready();
    dependent.ValueA.Name.ShouldBe(nameof(TestDependentWithDefaultValue));
    loadedCalled.ShouldBeTrue();
  }

  [Test]
  public void DependentRequestingSupertypeOfProvidedTypeThrows() {
    var provider = new TestProviderSubtype(new TestDummyValueA());
    var dependent = new TestDependentNodeOneValue();
    provider.AddChild(dependent);
    provider._Ready();
    Should.Throw<DependentRequestedSupertypeException>(
      () => dependent._Ready()
    );
  }
}
