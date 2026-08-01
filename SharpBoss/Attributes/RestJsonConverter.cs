using System;

namespace SharpBoss.Attributes;

/// <summary>
/// Registers a concrete <see cref="System.Text.Json.Serialization.JsonConverter"/> from an application assembly.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RestJsonConverter : Attribute
{
    /// <summary>
    /// Initializes a converter registration.
    /// </summary>
    /// <param name="order">Registration order. Lower values run first.</param>
    public RestJsonConverter(int order = 0)
    {
        Order = order;
    }

    /// <summary>
    /// Gets the registration order.
    /// </summary>
    public int Order { get; }
}
