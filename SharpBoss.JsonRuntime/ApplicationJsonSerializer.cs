using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using SharpBoss.Runtime;

namespace SharpBoss.JsonRuntime;

internal sealed class ApplicationJsonSerializer : IApplicationJsonSerializer
{
    private JsonSerializerOptions? _options;

    internal ApplicationJsonSerializer()
        : this(new JsonSerializerOptions())
    {
    }

    internal ApplicationJsonSerializer(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = new JsonSerializerOptions(options)
        {
            TypeInfoResolver = options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver(),
        };
    }

    public void RegisterConverter(Type converterType)
    {
        ArgumentNullException.ThrowIfNull(converterType);

        if (!typeof(JsonConverter).IsAssignableFrom(converterType))
        {
            throw new InvalidOperationException(
                $"Type '{converterType.FullName}' is marked as a JSON converter but does not derive from {nameof(JsonConverter)}.");
        }

        if (converterType.IsAbstract || converterType.ContainsGenericParameters)
        {
            throw new InvalidOperationException(
                $"JSON converter '{converterType.FullName}' must be concrete and closed.");
        }

        if (converterType.GetConstructor(Type.EmptyTypes) is null)
        {
            throw new InvalidOperationException(
                $"JSON converter '{converterType.FullName}' must have a public parameterless constructor.");
        }

        var converter = (JsonConverter?)Activator.CreateInstance(converterType)
            ?? throw new InvalidOperationException($"Could not create JSON converter '{converterType.FullName}'.");
        GetOptions().Converters.Add(converter);
    }

    public object? Deserialize(string value, Type targetType)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(targetType);
        return JsonSerializer.Deserialize(value, targetType, GetOptions());
    }

    public string Serialize(object? value, Type inputType)
    {
        ArgumentNullException.ThrowIfNull(inputType);
        return JsonSerializer.Serialize(value, inputType, GetOptions());
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _options, null);
    }

    private JsonSerializerOptions GetOptions()
    {
        return Volatile.Read(ref _options)
            ?? throw new ObjectDisposedException(nameof(ApplicationJsonSerializer));
    }
}
