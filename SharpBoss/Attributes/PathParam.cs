using System;

namespace SharpBoss.Attributes;

/// <summary>
/// Binds an endpoint argument from a request path parameter.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class PathParam : Attribute
{
    public PathParam(string? paramName = null)
    {
        ParamName = paramName;
    }

    public string? ParamName { get; }
}
