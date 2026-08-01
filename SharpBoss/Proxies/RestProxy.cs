using System;
using System.Collections.Generic;
using System.Reflection;
using SharpBoss.Attributes;
using SharpBoss.Logging;
using SharpBoss.Models;
using SharpBoss.Runtime;

namespace SharpBoss.Proxies;

internal sealed class RestProxy
{
    private readonly Dictionary<MethodInfo, ProxyMethod> _proxyMethods = new();
    private readonly object _instance;

    public RestProxy(
        Type endpointType,
        Dictionary<Type, object> injectables,
        IApplicationJsonSerializer jsonSerializer)
    {
        ArgumentNullException.ThrowIfNull(endpointType);
        ArgumentNullException.ThrowIfNull(injectables);
        ArgumentNullException.ThrowIfNull(jsonSerializer);

        _instance = Activator.CreateInstance(endpointType)
            ?? throw new InvalidOperationException($"Could not create endpoint type '{endpointType.FullName}'.");
        Logger.Info($"Creating proxy for {endpointType.FullName}.");

        InjectFields(endpointType, injectables);

        foreach (var method in endpointType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
        {
            if (method.GetCustomAttributes(inherit: true) is not { } attributes
                || !Array.Exists(attributes, attribute => attribute is IHTTPMethod))
            {
                continue;
            }

            _proxyMethods.Add(method, BuildProxyMethod(method, jsonSerializer));
        }
    }

    public object? CallMethod(MethodInfo method, RestRequest request)
    {
        if (!_proxyMethods.TryGetValue(method, out var proxyMethod))
        {
            throw new MissingMethodException(method.DeclaringType?.FullName, method.Name);
        }

        return method.Invoke(_instance, BuildParameters(proxyMethod, request));
    }

    private void InjectFields(Type endpointType, Dictionary<Type, object> injectables)
    {
        foreach (var field in endpointType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (field.GetCustomAttribute<Inject>() is null)
            {
                continue;
            }

            var fieldType = field.FieldType;
            if (!injectables.TryGetValue(fieldType, out var injectable))
            {
                Logger.Info($"Creating injectable instance for {fieldType.FullName}.");
                injectable = Activator.CreateInstance(fieldType)
                    ?? throw new InvalidOperationException($"Could not create injectable type '{fieldType.FullName}'.");
                injectables.Add(fieldType, injectable);
            }

            field.SetValue(_instance, injectable);
        }
    }

    private static ProxyMethod BuildProxyMethod(MethodInfo method, IApplicationJsonSerializer jsonSerializer)
    {
        var parameters = new List<ProxyParameterData>();

        foreach (var parameter in method.GetParameters())
        {
            if (parameter.ParameterType == typeof(RestRequest))
            {
                parameters.Add(new ProxyParameterData(
                    ProxyParameterRestType.Request,
                    parameter,
                    parameter.Name ?? string.Empty,
                    Parser: null));
                continue;
            }

            var restType = ProxyParameterRestType.Body;
            var parameterName = parameter.Name ?? string.Empty;

            if (parameter.GetCustomAttribute<QueryParam>() is { } queryParameter)
            {
                restType = ProxyParameterRestType.Query;
                parameterName = queryParameter.ParamName ?? parameterName;
            }
            else if (parameter.GetCustomAttribute<PathParam>() is { } pathParameter)
            {
                restType = ProxyParameterRestType.Path;
                parameterName = pathParameter.ParamName ?? parameterName;
            }

            Func<string, object?> parser = parameter.ParameterType == typeof(string)
                ? value => value
                : value => jsonSerializer.Deserialize(value, parameter.ParameterType);

            parameters.Add(new ProxyParameterData(restType, parameter, parameterName, parser));
        }

        return new ProxyMethod(parameters);
    }

    private static object?[] BuildParameters(ProxyMethod proxyMethod, RestRequest request)
    {
        var callParameters = new object?[proxyMethod.Parameters.Count];

        for (var index = 0; index < proxyMethod.Parameters.Count; index++)
        {
            var parameter = proxyMethod.Parameters[index];
            if (parameter.RestType == ProxyParameterRestType.Request)
            {
                callParameters[index] = request;
                continue;
            }

            string? parseData = parameter.RestType switch
            {
                ProxyParameterRestType.Body => request.Body,
                ProxyParameterRestType.Query => request.QueryString[parameter.ParameterName],
                ProxyParameterRestType.Path => null,
                _ => throw new InvalidOperationException($"Unsupported parameter source '{parameter.RestType}'."),
            };

            if (parseData is not null)
            {
                callParameters[index] = parameter.Parser!(parseData);
            }
            else if (parameter.Parameter.HasDefaultValue)
            {
                callParameters[index] = parameter.Parameter.DefaultValue;
            }
        }

        return callParameters;
    }

    private sealed record ProxyMethod(IReadOnlyList<ProxyParameterData> Parameters);

    private sealed record ProxyParameterData(
        ProxyParameterRestType RestType,
        ParameterInfo Parameter,
        string ParameterName,
        Func<string, object?>? Parser);

    private enum ProxyParameterRestType
    {
        Body,
        Path,
        Query,
        Request,
    }
}
