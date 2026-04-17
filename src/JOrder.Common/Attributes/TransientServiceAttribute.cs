namespace JOrder.Common.Attributes;

/// <summary>
/// Marks a class for automatic registration as a transient service.
/// The class will be registered against all of its non-framework interfaces.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class TransientServiceAttribute : Attribute;

