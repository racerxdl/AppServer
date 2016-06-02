using ASAttrib.Attributes;
using ASAttrib.Exceptions;
using ASAttrib.Models;
using ASTools.Logger;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Linq;

namespace ASAttrib.Processors {
  public class RestProcessor {
    private static Logger LOG = new Logger(typeof(RestProcessor));

    private readonly Type[] RestTypes = new Type[] { typeof(GET), typeof(POST), typeof(PUT), typeof(DELETE) };

    private Dictionary<string, Dictionary<string, RestCall>> endpoints;
    private Dictionary<string, object> instances;
    private Dictionary<string, IRestExceptionHandler> exceptionHandlers;

    public RestProcessor() {
      endpoints = new Dictionary<string, Dictionary<string, RestCall>>();
      instances = new Dictionary<string, object>();
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

      LOG.i("Initialized " + instances.Count + " REST instances.");
      LOG.i("Initialized " + endpoints.Keys.Count + " REST endpoints.");
      LOG.i("Initialized " + exceptionHandlers.Keys.Count + " Custom Exception Handlers");
    }

    public RestCall getEndPoint(string path, string method) {
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
      if (instances.ContainsKey(rc.methodClass.Name)) {
        /**
         * Parameters Processing
         * 
         * For now we're using Reflection to fill the parameters,
         * but in the future I think it is best to create a proxy class
         * to the target class, that receives RestRequest and calls the 
         * subsequent class with the correct parameters. Then we only 
         * do reflection at the setup
         */
        ParameterInfo[] parameters = rc.call.GetParameters();
        object[] methodParams = new object[parameters.Length];
        int i = 0;
        foreach (ParameterInfo param in parameters) {
          // Try query param
          Attribute pa = param.GetCustomAttribute(typeof(QueryParam));
          if (pa != null) {
            QueryParam q = (QueryParam)pa;
            string queryName = q.ParamName == null ? param.Name : q.ParamName;
            string queryData = request.QueryString[queryName];

            // If anyone knows a better way to do this. Please say it.
            // This is ugly
            if (!typeof(string).IsAssignableFrom(param.ParameterType)) {
              // Not String Parameter
              if (typeof(long).IsAssignableFrom(param.ParameterType)) {
                long output = 0;
                long.TryParse(queryData, out output);
                methodParams[i] = output;
              } else if (typeof(int).IsAssignableFrom(param.ParameterType)) {
                int output = 0;
                int.TryParse(queryData, out output);
                methodParams[i] = output;
              } else if (typeof(double).IsAssignableFrom(param.ParameterType)) {
                double output = 0;
                double.TryParse(queryData, out output);
                methodParams[i] = output;
              } else if (typeof(float).IsAssignableFrom(param.ParameterType)) {
                float output = 0;
                float.TryParse(queryData, out output);
                methodParams[i] = output;
              }
            } else {
              methodParams[i] = queryData;
            }

            i++;
            continue;
          }

          // Try Path param
          pa = param.GetCustomAttribute(typeof(PathParam));
          if (pa != null) {
            // TODO: Path Param
            i++;
            continue;
          }

          // If no match to our Attributes, then it is a body data. Try to deserialize
          if (request.BodyData != null) {
            if (typeof(string).IsAssignableFrom(param.ParameterType)) {
              methodParams[i] = request.BodyData;
            } else {
              methodParams[i] = JsonConvert.DeserializeObject(request.BodyData, param.ParameterType);
            }
          }
          i++;
        }

        // Calling the target method
        object ret = rc.call.Invoke(instances[rc.methodClass.Name], methodParams);
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
