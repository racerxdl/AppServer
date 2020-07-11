using System;
using System.Linq;
using System.Net;
using System.Threading;

using SharpBoss.Logging;
using SharpBoss.Models;
using SharpBoss.Processors;

namespace SharpBoss.Workers {
  /// <summary>
  /// The Boss HTTP Server
  /// </summary>
  internal class HttpServer {
    private readonly HttpListener _listener = new HttpListener ();
    private Func<HttpListenerRequest, RestResponse> _processor;
    private string _listenUrl;

    /// <summary>
    /// Instantiate Boss HTTP Server
    /// </summary>
    /// <param name="listenUrl">Base endpoint URL</param>
    public HttpServer (string listenUrl, Func<HttpListenerRequest, RestResponse> httpProcessor) {
      this._listenUrl = listenUrl;

      if (!HttpListener.IsSupported) {
        throw new NotSupportedException ("HTTP Listener is not supported");
      }

      this._processor = httpProcessor;

      this._listener.Prefixes.Add (this._listenUrl);
      this._listener.Start ();
    }

    /// <summary>
    /// Retrieve Listen URL
    /// </summary>
    public string ListenUrl {
      get { return this._listenUrl; }
    }

    /// <summary>
    /// Starts HTTP Server
    /// </summary>
    public void Run () {
      ThreadPool.QueueUserWorkItem ((o) => {
        Logger.Info ("HttpServer is running at " + this._listenUrl);

        try {
          while (this._listener.IsListening) {
            ThreadPool.QueueUserWorkItem ((item) => {
              var listenerContext = item as HttpListenerContext;

              try {
                Logger.Debug (listenerContext.Request.HttpMethod + " - " + listenerContext.Request.RawUrl);

                var response = this._processor (listenerContext.Request);

                listenerContext.Response.ContentType = response.ContentType;
                listenerContext.Response.StatusCode = (int)response.StatusCode;
                listenerContext.Response.ContentLength64 = response.Result.Length;
                listenerContext.Response.OutputStream.Write (response.Result, 0, response.Result.Length);
              } catch (Exception ex) {
                Logger.Error (string.Format ("Something went wrong. Reason: {0}", ex.Message));

                listenerContext.Response.ContentType = "application/json";
                listenerContext.Response.StatusCode = 500;
                listenerContext.Response.ContentLength64 = 0;
                //listenerContext.Response.OutputStream.Write (response.Result, 0, response.Result.Length);
              } finally {
                listenerContext.Response.OutputStream.Close ();
              }
            }, this._listener.GetContext ());
          }
        } catch (Exception ex) {
          Logger.Error (string.Format ("Something went wrong. Reason: {0}", ex.Message));
        }
      });
    }

    /// <summary>
    /// Stop the HTTP Server
    /// </summary>
    public void Stop () {
      _listener.Stop ();
      _listener.Close ();
    }
  }
}
