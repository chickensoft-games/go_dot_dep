namespace GoDotDep;
using System;

/// <summary>
/// Represents a dependency on a value provided by a provider node higher in the
/// current scene tree.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class DependencyAttribute : Attribute { }
