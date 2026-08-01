using System;

namespace SharpBoss.Attributes;

/// <summary>
/// Binds an endpoint argument from the query string.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class QueryParam : Attribute
{
    public QueryParam(string? paramName = null)
    {
        ParamName = paramName;
    }

    public string? ParamName { get; }
}
