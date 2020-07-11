using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

using SharpBoss.Attributes;
using SharpBoss.Models;
using SharpBoss.Logging;

namespace SharpBoss.Proxies {
  /// <summary>
  /// 
  /// </summary>
  internal class RestProxy {
    private static readonly Type[] _baseTypes = {
      typeof (int), typeof (float), typeof (long), typeof (double)
    };

    private Dictionary<string, ProxyMethod> _proxyMethods;
    private object _instance;
    private Type _classType;

    /// <summary>
    /// Create a new proxy between request and rest class to inject parameters
    /// </summary>
    /// <param name="restClass">Rest Class type</param>
    /// <param name="injectables">Injectables</param>
    internal RestProxy (Type restClass, Dictionary<string, Object> injectables) {
      this._instance = Activator.CreateInstance (restClass);
      this._classType = restClass;
      this._proxyMethods = new Dictionary<string, ProxyMethod> ();

      Logger.Info ("Creating proxy for " + restClass.Name);
      var restAttribute = restClass.GetCustomAttribute (typeof (REST));

      var rest = (REST) restAttribute;
      var fields = restClass.GetFields (BindingFlags.NonPublic | BindingFlags.Instance);

      foreach (var field in fields) {
        if (field.GetCustomAttribute (typeof (Inject)) != null) {
          var fieldType = field.FieldType;
          object injectableInstance;

          if (injectables.ContainsKey (fieldType.FullName)) {
            injectableInstance = injectables[fieldType.FullName];
          } else {
            Logger.Info ("Creating injectable instance for class " + fieldType.FullName);
            injectableInstance = Activator.CreateInstance (fieldType);
            injectables.Add (fieldType.Name, injectableInstance);
          }

          field.SetValue (this._instance, injectableInstance);
        }
      }

      var methods = restClass.GetMethods ();

      foreach (var method in methods) {
        this._proxyMethods.Add (method.Name, new ProxyMethod (method));

        foreach (var parameter in method.GetParameters ()) {
          var restType = ProxyParameterRestType.BODY;
          string lookName = parameter.Name;
          Attribute attribute;

          if ((attribute = parameter.GetCustomAttribute (typeof (QueryParam))) != null) {
            restType = ProxyParameterRestType.QUERY;

            if (((QueryParam)attribute).ParamName != null) {
              lookName = ((QueryParam)attribute).ParamName;
            }
          } else if ((attribute = parameter.GetCustomAttribute (typeof (PathParam))) != null) {
            restType = ProxyParameterRestType.PATH;

            if (((PathParam)attribute).ParamName != null) {
              lookName = ((PathParam)attribute).ParamName;
            }
          }

          Func<string, object> parser;
          Type baseType;

          if ((baseType = GetBaseType (parameter.ParameterType)) != null) {
            parser = item => {
              var property = new object[] { item, Activator.CreateInstance (baseType) };
              baseType.InvokeMember ("TryParse", BindingFlags.InvokeMethod, null, null, property);

              return property[1];
            };
          } else if (typeof (string).IsAssignableFrom (parameter.ParameterType)) {
            parser = item => item;
          } else {
            parser = item => JsonSerializer.Deserialize (item, parameter.ParameterType);
          }

          this._proxyMethods[method.Name].ProxyData.Add (
            new ProxyParameterData (restType, parameter.ParameterType, lookName, parser)
          );
        }
      }
    }

    /// <summary>
    /// Invoke method by name and REST Request from REST Class instance
    /// </summary>
    /// <param name="methodName">Method name</param>
    /// <param name="request">REST Request</param>
    /// <returns>Return response</returns>
    public object CallMethod (string methodName, RestRequest request) {
      return this._proxyMethods[methodName].Method.Invoke (
        this._instance, BuildParameters (methodName, request)
      );
    }

    /// <summary>
    /// Rrtrieve base type from type
    /// </summary>
    /// <param name="type">Type</param>
    /// <returns>Base Type or null</returns>
    private static Type GetBaseType (Type type) {
      try {
        return _baseTypes
               .Where (x => x.IsAssignableFrom (type))
               .ElementAt (0);
      } catch (ArgumentOutOfRangeException) {
        return null;
      }
    }

    /// <summary>
    /// Build paramters to send with REST Request
    /// </summary>
    /// <param name="methodName">Method name</param>
    /// <param name="request">REST Request</param>
    /// <returns>Array of parameters (Body, Path and Query)</returns>
    private object[] BuildParameters (string methodName, RestRequest request) {
      var proxyData = this._proxyMethods[methodName].ProxyData;
      var callParams = new object[proxyData.Count];

      for (var i = 0; i < proxyData.Count; i++) {
        string parseData = null;

        switch (proxyData[i].RestType) {
          case ProxyParameterRestType.BODY:
            parseData = request.Body;
            break;
          case ProxyParameterRestType.PATH:
            // TODO: Implement Path parameters to array
            break;
          case ProxyParameterRestType.QUERY:
            parseData = request.QueryString[proxyData[i].ParameterName];
            break;
        }

        if (parseData != null) {
          callParams[i] = proxyData[i].Parser (parseData);
        }
      }

      return callParams;
    }

    /// <summary>
    /// Proxy definition for REST Class method
    /// </summary>
    private class ProxyMethod {
      private MethodInfo _method;
      private List<ProxyParameterData> _proxyData;

      /// <summary>
      /// Create new proxy paramter data for method reflection
      /// </summary>
      /// <param name="method">Method info reflection</param>
      public ProxyMethod (MethodInfo method) {
        this._method = method;
        this._proxyData = new List<ProxyParameterData> ();
      }

      /// <summary>
      /// Get class method reflection
      /// </summary>
      public MethodInfo Method {
        get { return _method; }
      }

      /// <summary>
      /// Retrieve a list of proxy parameter data
      /// </summary>
      public List<ProxyParameterData> ProxyData {
        get { return _proxyData; }
      }
    }

    /// <summary>
    /// Proxy definition for parameter data
    /// </summary>
    private class ProxyParameterData {
      private ProxyParameterRestType _restType;
      private Type _parameterType;
      private string _paramterName;
      private Func<string, object> _parser;

      /// <summary>
      /// Create new parameter for proxy injection
      /// </summary>
      /// <param name="restType">REST parameter type</param>
      /// <param name="parameterType">Parameter type</param>
      /// <param name="paramterName">Name</param>
      /// <param name="parser">Paramter value parser</param>
      public ProxyParameterData (ProxyParameterRestType restType, Type parameterType, string paramterName, Func<string, object> parser) {
        this._restType = restType;
        this._parameterType = parameterType;
        this._paramterName = paramterName;
        this._parser = parser;
      }

      /// <summary>
      /// Retrieve REST parameter type
      /// </summary>
      public ProxyParameterRestType RestType {
        get { return _restType; }
      }

      /// <summary>
      /// Retrieve parameter type
      /// </summary>
      public Type ParameterType {
        get { return _parameterType; }
      }

      /// <summary>
      /// Retrieve parameter name
      /// </summary>
      public string ParameterName {
        get { return _paramterName; }
      }

      /// <summary>
      /// Retrieve parameter parser
      /// </summary>
      public Func<string, object> Parser {
        get { return _parser; }
      }
    }

    /// <summary>
    /// Type of REST Parameter
    /// </summary>
    private enum ProxyParameterRestType {
      BODY,
      PATH,
      QUERY
    }
  }
}