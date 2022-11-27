namespace GoDotDepTests;
using System.Diagnostics;
using Godot;
using GoDotDep;
using GoDotTest;
using Shouldly;

public partial class TestProviderNode : Node, IProvider<string> {
  public string Get() => "hello, world!";
}

public class IsProviderPerformanceTest : TestClass {

  private const long ITERATIONS = 250_000;

  public IsProviderPerformanceTest(Node testScene) : base(testScene) { }

  [Test]
  public void ReflectionTest() {
    // Since reflection can be slow, this is a performance check to make sure
    // it's running as fast as we'd expect.
    var stopwatch = new Stopwatch();
    var node = new TestProviderNode();
    var type = node.GetType();
    var genericType = node.Get().GetType();
    var success = false;
    stopwatch.Start();
    for (long i = 0; i < ITERATIONS; i++) {
      var isProvider = type.IsProvider(genericType);
      success = isProvider;
    }
    stopwatch.Stop();
    success.ShouldBeTrue();
    GD.Print("Reflection finished in " + stopwatch.Elapsed + " seconds.");
    GD.Print("Average time " + (stopwatch.Elapsed / ITERATIONS));
  }
}
