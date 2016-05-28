using ASAttrib.Models;
using ASTools.Logger;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net;
using System.Threading;

namespace AppServer.Server {
  class HttpServer {
    private readonly HttpListener _listener = new HttpListener();
    private Func<HttpListenerRequest, RestResult> httpProcesser;
    private string listenUrl;

    private static Logger LOG = new Logger(typeof(HttpServer));

    public HttpServer(string listenUrl, Func<HttpListenerRequest, RestResult> httpProcesser) {
      if (!HttpListener.IsSupported) {
        LOG.e("HTTP Listener is not supported.");
        throw new NotSupportedException("HTTP Listener is not supported");
      }

      this.listenUrl = listenUrl + (listenUrl.Last() != '/' ? "/" : "");
      this.httpProcesser = httpProcesser;

      _listener.Prefixes.Add(this.listenUrl);
      _listener.Start();
    }

    public void Run() {
      ThreadPool.QueueUserWorkItem((o) => {
        LOG.i("HttpServer is running at " + this.listenUrl);
        try {
          while (_listener.IsListening) {
            ThreadPool.QueueUserWorkItem((c) => {
              var ctx = c as HttpListenerContext;
              try {
                LOG.d(ctx.Request.HttpMethod + " - " + ctx.Request.RawUrl);
                String requestBody = "";
                JObject jbody = null;
                if (ctx.Request.HasEntityBody) {
                  using (System.IO.Stream body = ctx.Request.InputStream) {
                    using (System.IO.StreamReader reader = new System.IO.StreamReader(body, ctx.Request.ContentEncoding)) {
                      requestBody = reader.ReadToEnd();
                    }
                  }
                  if (ctx.Request.ContentType.Equals("application/json")) {
                    jbody = JObject.Parse(requestBody);
                  }
                }
                RestResult ret = httpProcesser(ctx.Request);
                ctx.Response.ContentType = ret.ContentType;
                ctx.Response.StatusCode = (int)ret.StatusCode;
                ctx.Response.ContentLength64 = ret.Result.Length;
                ctx.Response.OutputStream.Write(ret.Result, 0, ret.Result.Length);
              } catch (Exception e) {
                LOG.e(e.Message);
              } finally {
                ctx.Response.OutputStream.Close();
              }
            }, _listener.GetContext());
          }
        } catch (Exception e) {
          LOG.e(e.Message);
        }
      });
    }

    public void Stop() {
      _listener.Stop();
      _listener.Close();
    }
  }
}