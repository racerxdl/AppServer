using System;
using System.Collections.Specialized;
using System.Configuration;
using System.Net;
using System.Text;

using SharpBoss.Logging;
using SharpBoss.Models;
using SharpBoss.Workers;

namespace SharpBoss {
  /// <summary>
  /// SharBoss main class
  /// </summary>
  public class SharpBoss {
    HttpServer _server;
    ApplicationLoader _applicationLoader;
    dynamic _appSettings;

    private readonly string _defaultListenUrl = @"http://localhost:8080/";
    private readonly string _environmentVariable = "SHARPBOSS_URL";
    private readonly string _configKey = "SHARPBOSS_URL";

    /// <summary>
    /// SharpBoss initializer, the listen URL can be defined on:
    /// <list type="bullet">
    ///   <item>On environment: <b>SHARPBOSS_URL</b></item>
    ///   <item>On config as: <b>SHARPBOSS_URL</b></item>
    ///   <item>On constructor argument: <b>listenUrl</b></item>
    /// </list>
    /// </summary>
    /// <param name="listenUrl">Listen URL for base endpoint</param>
    /// <returns>New SharpBoss instance</returns>
    public SharpBoss (string listenUrl = null, dynamic appSettings = null) {
      SetAppSettings (appSettings);

      if (listenUrl == null) {
        listenUrl = GetListenUrl ();
      }

      this._applicationLoader = new ApplicationLoader ();

      listenUrl = listenUrl + (listenUrl.EndsWith ("/") ? "" : "/");
      this._server = new HttpServer (listenUrl, ProcessHttpCalls);
    }

    /// <summary>
    /// Define App settings collection when using configuration file
    /// </summary>
    /// <param name="appSettings">Application settings collection</param>
    private void SetAppSettings (dynamic appSettings = null) {
      if (appSettings == null) {
        this._appSettings = ConfigurationManager.AppSettings;
      } else {
        this._appSettings = appSettings;
      }
    }

    /// <summary>
    /// Do the trick, like a boss!
    /// </summary>
    public void Run () {
      this._server.Run ();
    }

    /// <summary>
    /// If u did the trick, like a boss. So it's the end D:
    /// </summary>
    public void Stop () {
      this._server.Stop ();
    }

    /// <summary>
    /// Retrieve HTTP Server Listen URL
    /// </summary>
    /// <returns>Listen URL</returns>
    public string GetHttpServerListenUrl () {
      return this._server.ListenUrl;
    }

    /// <summary>
    /// Process all HTTP calls and return the request response
    /// </summary>
    /// <param name="request">Http Listener</param>
    /// <returns>Rest response from request</returns>
    RestResponse ProcessHttpCalls (HttpListenerRequest request) {
      var ePath = request.Url.AbsolutePath.Split (
        new char[] { '/' }, 2, StringSplitOptions.RemoveEmptyEntries
      );

      var req = new RestRequest (request);

      if (ePath.Length == 0) {
        return new RestResponse ("No such endpoint.", "text/plain", HttpStatusCode.NotFound);
      } else {
        var path = ePath.Length > 1 ? "/" + ePath[1] : "/";
        var method = request.HttpMethod;
        var app = ePath[0];

        Logger.Debug (string.Format ("Received request for: APP={0} {1} {2}", app, method, path));

        if (this._applicationLoader.ContainsEndPoint (app, path, method)) {
          try {
            return this._applicationLoader.Process (app, path, method, req);
          } catch (Exception ex) {
            var response = new RestResponse ();
            var exceptionMessage = "";

            if (ex.InnerException != null) {
              exceptionMessage = ex.InnerException.ToString ();
            } else {
              exceptionMessage = ex.ToString ();
            }

            Logger.Error (string.Format (
              "Exception when calling application {0} in endpoint {1} {2}\r\n{3}",
              app, method, path, exceptionMessage
            ));

            response.StatusCode = HttpStatusCode.InternalServerError;
            response.ContentType = "text/plain";
            response.Result = Encoding.UTF8.GetBytes (exceptionMessage);

            return response;
          }
        } else {
          return new RestResponse ("No such endpoint.", "text/plain", HttpStatusCode.NotFound);
        }
      }
    }

    /// <summary>
    /// Retrieve base endpoint from configuration/environment/default
    /// </summary>
    /// <returns>Listen URL</returns>
    private string GetListenUrl () {
      var environmentVariable = Environment.GetEnvironmentVariable (_environmentVariable);
      var configValue = this._appSettings[_configKey];

      if (environmentVariable != null) {
        return environmentVariable;
      } else if (configValue != null) {
        return configValue.Value;
      }

      return _defaultListenUrl;
    }
  }
}