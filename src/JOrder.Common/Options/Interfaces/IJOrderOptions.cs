namespace JOrder.Common.Options.Interfaces;

/// <summary>
/// Marker interface for JOrder options classes.
/// Implementing types must expose a static <see cref="SectionName"/> property
/// that identifies their configuration section path, enabling generic binding
/// via <c>AddJOrderOptions&lt;T&gt;()</c>.
/// </summary>
public interface IJOrderOptions
{
    /// <summary>
    /// The configuration section path this options class binds to (e.g. <c>"JOrder:DatabaseOptions"</c>).
    /// </summary>
    static abstract string SectionName { get; }
}