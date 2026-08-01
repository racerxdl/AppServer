using System;
using System.Reflection;
using SharpBoss.Attributes;

namespace SharpBoss.Models;

internal sealed class RestCall
{
    public RestCall(Type endpointType, MethodInfo method, IHTTPMethod httpMethod, REST restClass)
    {
        EndpointType = endpointType;
        Method = method;
        HttpMethod = httpMethod;
        RestClass = restClass;
    }

    public Type EndpointType { get; }

    public MethodInfo Method { get; }

    public IHTTPMethod HttpMethod { get; }

    public REST RestClass { get; }
}
