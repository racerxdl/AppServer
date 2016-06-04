using ASAttrib.Attributes;
using ASAttrib.Models;
using ASTools.Logger;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ASAttrib.Proxy {
  internal class RestProxy {
    private static readonly Type[] baseTypes = { typeof(int), typeof(float), typeof(long), typeof(double) };

    private static Logger LOG = new Logger(typeof(RestProxy));

    private Dictionary<string, ProxyMethod> proxyMethods;
    private object instance;
    private Type classType;

    internal RestProxy(Type restClass) {
      instance = Activator.CreateInstance(restClass);
      classType = restClass;
      Attribute t = restClass.GetCustomAttribute(typeof(REST));
      LOG.i("Creating proxy for " + restClass.Name);
      proxyMethods = new Dictionary<string, ProxyMethod>();
      REST trest = (REST)t;
      MethodInfo[] methods = restClass.GetMethods();
      foreach (var methodInfo in methods) {
        proxyMethods.Add(methodInfo.Name, new ProxyMethod(methodInfo));
        foreach (var paramInfo in methodInfo.GetParameters()) {
          // Default to body param
          ProxyParameterRestType restType = ProxyParameterRestType.BODY;
          string lookName = paramInfo.Name;
          Attribute p;

          if ( (p = paramInfo.GetCustomAttribute(typeof(QueryParam))) != null) {
            restType = ProxyParameterRestType.QUERY;
            lookName = ((QueryParam)p).ParamName != null ? ((QueryParam)p).ParamName : paramInfo.Name;
          } else if ( (p = paramInfo.GetCustomAttribute(typeof(PathParam))) != null) {
            restType = ProxyParameterRestType.PATH;
            lookName = ((PathParam)p).ParamName != null ? ((PathParam)p).ParamName : paramInfo.Name;
          }

          Func<string, object> parser;
          Type baseType;

          if ((baseType = getBaseType(paramInfo.ParameterType)) != null) {
            parser = x => {
              object[] dp = new object[] { x, Activator.CreateInstance(baseType) };
              baseType.InvokeMember("TryParse", BindingFlags.InvokeMethod, null, null, dp);
              return dp[1];
            };
          } else if (typeof(string).IsAssignableFrom(paramInfo.ParameterType)) {
            parser = x => x;
          } else {
            parser = x => JsonConvert.DeserializeObject(x, paramInfo.ParameterType);
          }

          proxyMethods[methodInfo.Name].ProxyData.Add(new ProxyParameterData(restType, paramInfo.ParameterType, lookName, parser));
        }
      }
    }

    public object callMethod(string methodName, RestRequest request) {
      return proxyMethods[methodName].Method.Invoke(instance, paramsBuilder(methodName, request));
    }

    private static Type getBaseType(Type t) {
      try {
        return baseTypes.Where(x => x.IsAssignableFrom(t)).ElementAt(0);
      } catch (ArgumentOutOfRangeException) {
        return null;
      }
    }

    private object[] paramsBuilder(string methodName, RestRequest request) {
      List<ProxyParameterData> proxyData = proxyMethods[methodName].ProxyData;
      object[] callParams = new object[proxyData.Count];
      for (int i=0; i<proxyData.Count; i++) {

        string parseData = null;

        switch (proxyData[i].RestType) {
          case ProxyParameterRestType.BODY:
            parseData = request.BodyData;
            break;
          case ProxyParameterRestType.PATH:
            // TODO
            break;
          case ProxyParameterRestType.QUERY:
            parseData = request.QueryString[proxyData[i].LookName];
            break;
        }

        if (parseData != null) {
          callParams[i] = proxyData[i].Parse(parseData);
        }
      }

      return callParams;
    }

    private class ProxyMethod {
      private MethodInfo method;
      private List<ProxyParameterData> proxyData;

      public MethodInfo Method {
        get { return method; }
      }

      public List<ProxyParameterData> ProxyData {
        get { return proxyData; }
      }

      public ProxyMethod(MethodInfo method) {
        this.method = method;
        this.proxyData = new List<ProxyParameterData>();
      }
    }

    private class ProxyParameterData {
      private ProxyParameterRestType restType;
      private Type parameterType;
      private string lookName;
      private Func<string, object> parse;

      public ProxyParameterRestType RestType {
        get { return restType; }
      }

      public Type ParameterType {
        get { return parameterType; }
      }

      public string LookName {
        get { return lookName; }
      }

      public Func<string, object> Parse {
        get { return parse; }
      }

      public ProxyParameterData(ProxyParameterRestType restType, Type parameterType, string lookName, Func<string, object> parse) {
        this.restType = restType;
        this.parameterType = parameterType;
        this.lookName = lookName;
        this.parse = parse;
      }
    }

    private enum ProxyParameterRestType {
      BODY,
      PATH,
      QUERY
    }
  }
}
