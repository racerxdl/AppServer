using System;

namespace SharpBoss.Exceptions;

public sealed class DuplicateRestMethodException : Exception
{
    public DuplicateRestMethodException()
    {
    }

    public DuplicateRestMethodException(string message)
        : base(message)
    {
    }

    public DuplicateRestMethodException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
