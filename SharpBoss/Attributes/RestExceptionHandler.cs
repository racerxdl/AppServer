using System;

namespace SharpBoss.Attributes;

/// <summary>
/// Registers an exception handler for a specific exception type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class RestExceptionHandler : Attribute
{
    public RestExceptionHandler(Type exceptionType)
    {
        ArgumentNullException.ThrowIfNull(exceptionType);
        if (!typeof(Exception).IsAssignableFrom(exceptionType))
        {
            throw new ArgumentException("The handler target must derive from Exception.", nameof(exceptionType));
        }

        ExceptionType = exceptionType;
    }

    public Type ExceptionType { get; }
}
