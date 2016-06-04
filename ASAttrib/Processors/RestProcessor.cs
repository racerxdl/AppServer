using ASAttrib.Attributes;
using ASAttrib.Exceptions;
using ASAttrib.Models;
using ASTools.Logger;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using ASAttrib.Proxy;

namespace ASAttrib.Processors {
  public class RestProcessor {
    private static Logger LOG = new Logger(typeof(RestProcessor));

    private readonly Type[] RestTypes = new Type[] { typeof(GET), typeof(POST), typeof(PUT), typeof(DELETE) };

    private Dictionary<string, Dictionary<string, RestCall>> endpoints;
    private Dictionary<string, RestProxy> proxies;
    private Dictionary<string, IRestExceptionHandler> exceptionHandlers;

    public RestProcessor() {
      endpoints = new Dictionary<string, Dictionary<string, RestCall>>();
      proxies = new Dictionary<string, RestProxy>();
      exceptionHandlers = new Dictionary<string, IRestExceptionHandler>();
    }

    public void init(Assembly runningAssembly, string modulesAssembly) {
      Type[] typelist = Tools.GetTypesInNamespace(runningAssembly, modulesAssembly);
      for (int i = 0; i < typelist.Length; i++) {
        Type tClass = typelist[i];

        // Search for REST Attribute
        Attribute t = tClass.GetCustomAttribute(typeof(REST));
        if (t != null) {
          LOG.i("Found REST class " + tClass.Name);
          REST trest = (REST)t;
          proxies.Add(tClass.Name, new RestProxy(tClass));

          MethodInfo[] methods = tClass.GetMethods();
          foreach (var methodInfo in methods) {
            foreach (Type rt in RestTypes) {
              Attribute rta = methodInfo.GetCustomAttribute(rt);
              if (rta != null) {
                RestCall restCall = new RestCall();
                try {
                  restCall.className = tClass.Name;
                  restCall.methodName = methodInfo.Name;
                  restCall.method = (HTTPMethod)rta;
                  restCall.baseRest = trest;

                  LOG.i("     Registering method " + methodInfo.Name + " for " + restCall.method.Method + " " + trest.Path  + restCall.method.Path);

                  addEndpoint(restCall);
                } catch (DuplicateRestMethodException) {
                  LOG.e("DuplicateRestMethodException: There is already a " + restCall.method.Method + " for " + trest.Path  + restCall.method.Path + " registered.");
                }
              }
            }
          }
        }

        // Search for RestExceptionHandler Attribute
        t = tClass.GetCustomAttribute(typeof(RestExceptionHandler));
        if ( t != null ) {
          LOG.i("Found a RestExceptionHandler " + tClass.Name);
          if (typeof(IRestExceptionHandler).IsAssignableFrom(tClass)) {
            RestExceptionHandler reh = (RestExceptionHandler)t;
            if (typeof(Exception).IsAssignableFrom(reh.exceptionType)) {
              IRestExceptionHandler handler = (IRestExceptionHandler)Activator.CreateInstance(tClass);
              exceptionHandlers.Add(reh.ExceptionType.Name, handler);
              LOG.i("     Registered a custom exception handler for exception \"" + reh.ExceptionType.Name + "\" for class " + tClass.Name);
            } else {
              LOG.e("     Class " + tClass.Name + " contains the \"RestExceptionHandler\" attribute the passed type does not inherit Exception class. Skipping it.");
            }
          } else {
            LOG.e("     Class " + tClass.Name + " contains the \"RestExceptionHandler\" attribute but does not implement IRestExceptionHandler. Skipping it.");
          }
        }
      }

      LOG.i("Initialized " + proxies.Count + " REST proxies.");
      LOG.i("Initialized " + endpoints.Keys.Count + " REST endpoints.");
      LOG.i("Initialized " + exceptionHandlers.Keys.Count + " Custom Exception Handlers");
    }

    private RestCall getEndPoint(string path, string method) {
      if (endpoints.ContainsKey(path) && endpoints[path].ContainsKey(method)) {
        return endpoints[path][method];
      }

      return null;
    }

    public IRestExceptionHandler getExceptionHandler(string exceptionName) {
      if (exceptionHandlers.ContainsKey(exceptionName)) {
        return exceptionHandlers[exceptionName];
      } else {
        return null;
      }
    }

    public RestResult callEndPoint(string path, string method, RestRequest request) {
      RestCall rc = getEndPoint(path, method);
      if (proxies.ContainsKey(rc.className)) {
        object ret = proxies[rc.className].callMethod(rc.methodName, request);

        if (ret is string) {
          return new RestResult((string)ret);
        } else {
          return new RestResult(JsonConvert.SerializeObject(ret), "application/json");
        }

      }
      return new RestResult("Not found", "text/plain", HttpStatusCode.NotFound);
    }

    public bool containsEndPoint(string path, string method) {
      return endpoints.ContainsKey(path) && endpoints[path].ContainsKey(method);
    }

    private void addEndpoint(RestCall restCall) {
      string path = restCall.baseRest.Path + restCall.method.Path;
      if (!endpoints.ContainsKey(path)) {
        endpoints.Add(path, new Dictionary<string, RestCall>());
      }

      if (endpoints[path].ContainsKey(restCall.method.Method)) {
        throw new DuplicateRestMethodException();
      }

      endpoints[path][restCall.method.Method] = restCall;
    }
  }
}
