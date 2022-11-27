namespace GoDotDepTests;
using System.Collections.Generic;
using Godot;
using GoDotDep;
using GoDotTest;
using Shouldly;

public class IDependentExtensionTest : TestClass {
  public IDependentExtensionTest(Node testScene) : base(testScene) { }

  [Test]
  public void IsProviderRecognizesInterface()
    => typeof(IProvider<string>).IsProvider(typeof(string)).ShouldBeTrue();

  [Test]
  public void IsProviderIsFalseOnNonProvider()
    => typeof(IList<string>).IsProvider(typeof(string)).ShouldBeFalse();
}
