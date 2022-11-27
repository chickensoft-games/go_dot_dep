namespace GoDotDepTests;
using Godot;
using GoDotDep;
using GoDotTest;
using LightMock.Generator;
using Shouldly;

public class IProviderExtensionTest : TestClass {
  public IProviderExtensionTest(Node testScene) : base(testScene) { }

  [Test]
  public void AnnounceDoesNothingWhenNoEvents() {
    var node = new Mock<IProviderNode>();
    var state = new ProviderState();
    Should.NotThrow(() => state.Announce(node.Object));
  }
}
