using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.Json;

using SharpBoss.Attributes;
using SharpBoss.Attributes.Methods;
using SharpBoss.Exceptions;
using SharpBoss.Logging;
using SharpBoss.Models;
using SharpBoss.Proxies;

namespace SharpBoss.Processors {
  /// <summary>
  /// Process Rest calls from Rest classes inside namespace
  /// </summary>
  public class RestProcessor {
    private readonly Type[] _restTypes = new Type[] {
      typeof (GET), typeof (POST), typeof (PUT), typeof (DELETE)
    };

    private Dictionary<string, Dictionary<string, RestCall>> _endpoints;
    private Dictionary<string, RestProxy> _proxies;
    private Dictionary<string, IRestExceptionHandler> _exceptionHandlers;
    private Dictionary<string, object> _injectables;

    /// <summary>
    /// Create new REST processor
    /// </summary>
    public RestProcessor () {
      this._endpoints = new Dictionary<string, Dictionary<string, RestCall>> ();
      this._proxies = new Dictionary<string, RestProxy> ();
      this._exceptionHandlers = new Dictionary<string, IRestExceptionHandler> ();
      this._injectables = new Dictionary<string, object> ();
    }

    /// <summary>
    /// Intialize proxies from namespace and running assembly
    /// </summary>
    /// <param name="runningAssembly">Current running assembly</param>
    /// <param name="nameSpace">Namespace from running application</param>
    public void Init (Assembly runningAssembly, string nameSpace) {
      Logger.Info ("Retrieving array of types from namespace: " + nameSpace);
      var types = Tools.GetTypesInNamespace (runningAssembly, nameSpace);
      Logger.Info (string.Format("Found {0} types inside namespace {1}", types.Count, nameSpace));

      foreach (var type in types) {
        var restAttribute = type.GetCustomAttribute (typeof (REST));

        if (restAttribute != null) {
          Logger.Info ("Found REST class " + type.Name);
          var rest = (REST)restAttribute;

          this._proxies.Add (type.Name, new RestProxy (type, this._injectables));

          var methods = type.GetMethods ();

          foreach (var method in methods) {
            foreach (var restType in this._restTypes) {
              var httpMethodAttribute = method.GetCustomAttribute (restType);

              if (httpMethodAttribute != null) {
                var restCall = new RestCall (type.Name, method.Name, (IHTTPMethod) httpMethodAttribute, rest);

                try {
                  Logger.Info (string.Format (
                    "Registering method {0} for {1} {2}{3}",
                    method.Name,
                    restCall.HttpMethod.Method,
                    rest.Path,
                    restCall.HttpMethod.Path
                  ));

                  this.AddEndpoint (restCall);
                } catch (DuplicateRestMethodException) {
                  Logger.Error (string.Format(
                    "DuplicateRestMethodException: There is already a {0} for {1}{2} registered.",
                    restCall.HttpMethod.Method,
                    rest.Path,
                    restCall.HttpMethod.Path
                  ));
                }
              }
            }
          }
        }
      }
    }

    /// <summary>
    /// Retrieve Rest Call struct from path and HTTP method
    /// </summary>
    /// <param name="path">Request path</param>
    /// <param name="method">HTTP method</param>
    /// <returns>Rest Call struct</returns>
    private RestCall GetEndPoint (string path, string method) {
      if (this._endpoints.ContainsKey (path) && this._endpoints[path].ContainsKey (method)) {
        return this._endpoints[path][method];
      }

      return null;
    }

    /// <summary>
    /// Retrieve exception handler from REST Class by name
    /// </summary>
    /// <param name="exceptionName">Exception name</param>
    /// <returns>Rest exception handler</returns>
    public IRestExceptionHandler GetExceptionHandler (string exceptionName) {
      if (this._exceptionHandlers.ContainsKey (exceptionName)) {
        return this._exceptionHandlers[exceptionName];
      }
      else {
        return null;
      }
    }

    /// <summary>
    /// Process endpoint request and retrieve a Rest Response properly
    /// </summary>
    /// <param name="path">Request path</param>
    /// <param name="method">HTTP method</param>
    /// <param name="request">Rest Request</param>
    /// <returns>Rest Response</returns>
    public RestResponse Process (string path, string method, RestRequest request) {
      var restCall = GetEndPoint (path, method);

      if (restCall == null) {
        return new RestResponse ("Not found", "text/plain", HttpStatusCode.NotFound);
      }

      if (this._proxies.ContainsKey (restCall.ClassName)) {
        var response = this._proxies[restCall.ClassName].CallMethod (restCall.MethodName, request);

        if (response is string) {
          return new RestResponse ((string)response);
        } else {
          return new RestResponse (JsonSerializer.Serialize (response), "application/json");
        }
      } else {
        return new RestResponse ("Not found", "text/plain", HttpStatusCode.NotFound);
      }
    }

    /// <summary>
    /// Retrieve if endpoint is already defined
    /// </summary>
    /// <param name="path">Request path</param>
    /// <param name="method">HTTP method</param>
    /// <returns>True or false</returns>
    public bool ContainsEndPoint (string path, string method) {
      return this._endpoints.ContainsKey (path) && this._endpoints[path].ContainsKey (method);
    }

    /// <summary>
    /// Add REST Call endpoint to list of endpoints from namespace
    /// </summary>
    /// <param name="restCall">Rest Call struct</param>
    private void AddEndpoint (RestCall restCall) {
      string requestPath = restCall.RestClass.Path + restCall.HttpMethod.Path;

      if (!this._endpoints.ContainsKey (requestPath)) {
        this._endpoints.Add (requestPath, new Dictionary<string, RestCall> ());
      }

      if (this._endpoints[requestPath].ContainsKey (restCall.HttpMethod.Method)) {
        throw new DuplicateRestMethodException ();
      }

      this._endpoints[requestPath][restCall.HttpMethod.Method] = restCall;
    }
  }
}