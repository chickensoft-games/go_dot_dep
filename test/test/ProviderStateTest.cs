using Godot;
using GoDotDep;
using GoDotTest;
using Moq;

public class IProviderExtensionTest : TestClass {
  public IProviderExtensionTest(Node testScene) : base(testScene) { }

  [Test]
  public void AnnounceDoesNothingWhenNoEvents() {
    var node = new Mock<IProviderNode>();
    var state = new ProviderState();
    state.Announce(node.Object);
    // nothing to expect
  }
}
