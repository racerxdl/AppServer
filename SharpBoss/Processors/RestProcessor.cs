using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.Json;
using SharpBoss.Attributes;
using SharpBoss.Attributes.Methods;
using SharpBoss.Exceptions;
using SharpBoss.JsonRuntime;
using SharpBoss.Logging;
using SharpBoss.Models;
using SharpBoss.Proxies;
using SharpBoss.Runtime;

namespace SharpBoss.Processors;

/// <summary>
/// Discovers and executes REST endpoints from application assemblies.
/// </summary>
public sealed class RestProcessor : IDisposable
{
    private static readonly Type[] RestMethodAttributes =
    [
        typeof(GET),
        typeof(POST),
        typeof(PUT),
        typeof(DELETE),
    ];

    private readonly Dictionary<string, Dictionary<string, RestCall>> _endpoints = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, RestProxy> _proxies = new();
    private readonly Dictionary<Type, IRestExceptionHandler> _exceptionHandlers = new();
    private readonly Dictionary<Type, object> _injectables = new();
    private IApplicationJsonSerializer? _jsonSerializer;

    /// <summary>
    /// Creates a processor using default JSON serialization settings.
    /// </summary>
    public RestProcessor()
        : this(new ApplicationJsonSerializer())
    {
    }

    /// <summary>
    /// Creates a processor using a private clone of the supplied JSON settings.
    /// </summary>
    public RestProcessor(JsonSerializerOptions serializerOptions)
        : this(new ApplicationJsonSerializer(serializerOptions))
    {
    }

    internal RestProcessor(IApplicationJsonSerializer jsonSerializer)
    {
        ArgumentNullException.ThrowIfNull(jsonSerializer);
        _jsonSerializer = jsonSerializer;
    }

    internal int EndpointCount => _endpoints.Sum(endpoint => endpoint.Value.Count);

    /// <summary>
    /// Discovers all supported application types in an assembly.
    /// </summary>
    public void Init(Assembly runningAssembly)
    {
        ArgumentNullException.ThrowIfNull(runningAssembly);
        InitTypes(runningAssembly.GetTypes());
    }

    /// <summary>
    /// Discovers supported application types in one namespace.
    /// </summary>
    public void Init(Assembly runningAssembly, string nameSpace)
    {
        ArgumentNullException.ThrowIfNull(runningAssembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameSpace);
        InitTypes(runningAssembly.GetTypes().Where(type => string.Equals(type.Namespace, nameSpace, StringComparison.Ordinal)));
    }

    /// <summary>
    /// Gets the most specific registered handler for an exception type.
    /// </summary>
    public IRestExceptionHandler? GetExceptionHandler(Type exceptionType)
    {
        ArgumentNullException.ThrowIfNull(exceptionType);

        for (var currentType = exceptionType; currentType is not null; currentType = currentType.BaseType)
        {
            if (_exceptionHandlers.TryGetValue(currentType, out var handler))
            {
                return handler;
            }
        }

        return null;
    }

    /// <summary>
    /// Processes an endpoint request.
    /// </summary>
    public RestResponse Process(string path, string method, RestRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var restCall = GetEndPoint(path, method);

        if (restCall is null || !_proxies.TryGetValue(restCall.EndpointType, out var proxy))
        {
            return new RestResponse("Not found", "text/plain", HttpStatusCode.NotFound);
        }

        var response = proxy.CallMethod(restCall.Method, request);
        if (response is string text)
        {
            return new RestResponse(text);
        }

        return new RestResponse(
            GetJsonSerializer().Serialize(response, restCall.Method.ReturnType),
            "application/json");
    }

    /// <summary>
    /// Gets whether an endpoint is registered.
    /// </summary>
    public bool ContainsEndPoint(string path, string method)
    {
        return _endpoints.TryGetValue(path, out var methods) && methods.ContainsKey(method);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _endpoints.Clear();
        _proxies.Clear();
        _exceptionHandlers.Clear();
        _injectables.Clear();
        _jsonSerializer?.Dispose();
        _jsonSerializer = null;
    }

    private void InitTypes(IEnumerable<Type> discoveredTypes)
    {
        var types = discoveredTypes
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        RegisterConverters(types);
        RegisterExceptionHandlers(types);

        foreach (var type in types)
        {
            RegisterEndpoint(type);
        }
    }

    private void RegisterConverters(IEnumerable<Type> types)
    {
        var registrations = types
            .Select(type => new
            {
                Type = type,
                Attribute = type.GetCustomAttribute<RestJsonConverter>(),
            })
            .Where(registration => registration.Attribute is not null)
            .OrderBy(registration => registration.Attribute!.Order)
            .ThenBy(registration => registration.Type.FullName, StringComparer.Ordinal);

        foreach (var registration in registrations)
        {
            var converterType = registration.Type;
            GetJsonSerializer().RegisterConverter(converterType);
            Logger.Info($"Registered JSON converter {converterType.FullName}.");
        }
    }

    private void RegisterExceptionHandlers(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            var attributes = type.GetCustomAttributes<RestExceptionHandler>(inherit: false).ToArray();
            if (attributes.Length == 0)
            {
                continue;
            }

            if (!typeof(IRestExceptionHandler).IsAssignableFrom(type) || type.IsAbstract || type.ContainsGenericParameters)
            {
                throw new InvalidOperationException(
                    $"Exception handler '{type.FullName}' must be a concrete {nameof(IRestExceptionHandler)} implementation.");
            }

            var handler = (IRestExceptionHandler?)Activator.CreateInstance(type)
                ?? throw new InvalidOperationException($"Could not create exception handler '{type.FullName}'.");

            foreach (var attribute in attributes)
            {
                if (!typeof(Exception).IsAssignableFrom(attribute.ExceptionType))
                {
                    throw new InvalidOperationException(
                        $"'{attribute.ExceptionType.FullName}' is not an exception type.");
                }

                if (!_exceptionHandlers.TryAdd(attribute.ExceptionType, handler))
                {
                    throw new InvalidOperationException(
                        $"An exception handler is already registered for '{attribute.ExceptionType.FullName}'.");
                }

                Logger.Info($"Registered {type.FullName} for {attribute.ExceptionType.FullName}.");
            }
        }
    }

    private void RegisterEndpoint(Type type)
    {
        var rest = type.GetCustomAttribute<REST>();
        if (rest is null)
        {
            return;
        }

        Logger.Info($"Found REST class {type.FullName}.");
        var jsonSerializer = GetJsonSerializer();
        if (!_proxies.TryAdd(type, new RestProxy(type, _injectables, jsonSerializer)))
        {
            throw new InvalidOperationException($"REST class '{type.FullName}' was registered more than once.");
        }

        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
        {
            foreach (var restMethodAttribute in RestMethodAttributes)
            {
                if (method.GetCustomAttribute(restMethodAttribute) is not IHTTPMethod httpMethod)
                {
                    continue;
                }

                var restCall = new RestCall(type, method, httpMethod, rest);
                Logger.Info($"Registering {method.Name} for {httpMethod.Method} {rest.Path}{httpMethod.Path}.");
                AddEndpoint(restCall);
            }
        }
    }

    private RestCall? GetEndPoint(string path, string method)
    {
        return _endpoints.TryGetValue(path, out var methods) && methods.TryGetValue(method, out var endpoint)
            ? endpoint
            : null;
    }

    private void AddEndpoint(RestCall restCall)
    {
        var requestPath = restCall.RestClass.Path + restCall.HttpMethod.Path;
        if (!_endpoints.TryGetValue(requestPath, out var methods))
        {
            methods = new Dictionary<string, RestCall>(StringComparer.OrdinalIgnoreCase);
            _endpoints.Add(requestPath, methods);
        }

        if (!methods.TryAdd(restCall.HttpMethod.Method, restCall))
        {
            throw new DuplicateRestMethodException(
                $"Endpoint '{restCall.HttpMethod.Method} {requestPath}' is already registered.");
        }
    }

    private IApplicationJsonSerializer GetJsonSerializer()
    {
        return _jsonSerializer ?? throw new ObjectDisposedException(nameof(RestProcessor));
    }
}
