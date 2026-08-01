using System;

namespace SharpBoss.Runtime;

internal interface IApplicationJsonSerializer : IDisposable
{
    void RegisterConverter(Type converterType);

    object? Deserialize(string value, Type targetType);

    string Serialize(object? value, Type inputType);
}
