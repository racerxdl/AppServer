using ASAttrib.Attributes;
using ASAttrib.Exceptions;
using ASAttrib.Models;
using ASTools.Logger;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;

namespace ASAttrib.Processors {
  public class RestProcessor {
    private static Logger LOG = new Logger(typeof(RestProcessor));

    private readonly Type[] RestTypes = new Type[] { typeof(GET), typeof(POST), typeof(PUT), typeof(DELETE) };

    private Dictionary<string, Dictionary<string, RestCall>> endpoints;
    private Dictionary<string, object> instances;

    public RestProcessor() {
      endpoints = new Dictionary<string, Dictionary<string, RestCall>>();
      instances = new Dictionary<string, object>();
    }

    public void init(Assembly runningAssembly, string modulesAssembly) {
      Type[] typelist = Tools.GetTypesInNamespace(runningAssembly, modulesAssembly);
      for (int i = 0; i < typelist.Length; i++) {
        Type tClass = typelist[i];
        Attribute t = tClass.GetCustomAttribute(typeof(Rest));
        if (t != null) {
          LOG.i("Found REST class " + tClass.Name);
          Rest trest = (Rest)t;
          object instance = Activator.CreateInstance(tClass);
          instances.Add(tClass.Name, instance);
          MethodInfo[] methods = tClass.GetMethods();
          foreach (var methodInfo in methods) {
            foreach (Type rt in RestTypes) {
              Attribute rta = methodInfo.GetCustomAttribute(rt);
              if (rta != null) {
                RestCall restCall = new RestCall();
                try {
                  restCall.methodClass = tClass;
                  restCall.call = methodInfo;
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
      }

      LOG.i("Initialized " + instances.Count + " REST instances.");
      LOG.i("Initialized " + endpoints.Keys.Count + " REST endpoints.");
    }

    public RestCall getEndPoint(string path, string method) {
      if (endpoints.ContainsKey(path) && endpoints[path].ContainsKey(method)) {
        return endpoints[path][method];
      }

      return null;
    }

    public RestResult callEndPoint(string path, string method, RestRequest request) {
      RestCall rc = getEndPoint(path, method);
      if (instances.ContainsKey(rc.methodClass.Name)) {
        object ret = rc.call.Invoke(instances[rc.methodClass.Name], new object[] { request });
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
