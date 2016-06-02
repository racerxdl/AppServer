using ASAttrib.Models;
using ASAttrib.Processors;
using ASTools.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AppServer.Server {
  class ASRunner {
    
    HttpServer server;
    ApplicationManager appManager;
    private static Logger LOG = new Logger(typeof(ASRunner));

    public ASRunner() {
      server = new HttpServer("http://localhost:8080/", processHttpCalls);
      appManager = new ApplicationManager();
      if (ASTools.Tools.getRunningPlatform() == ASTools.Platform.Windows) {
        Console.WindowWidth = 180;
      }
    }

    public void run() {
      server.Run();
      Console.ReadLine();
    }
    RestResult processHttpCalls(HttpListenerRequest request) {
      string[] ePath = request.Url.AbsolutePath.Split(new char[] { '/' }, 2, StringSplitOptions.RemoveEmptyEntries);
      RestRequest req = new RestRequest(request);
      if (ePath.Length == 0) {
        return new RestResult("No such endpoint.", "text/plain", HttpStatusCode.NotFound);
      } else {
        string path = ePath.Length > 1 ? "/" + ePath[1] : "/";
        string method = request.HttpMethod;
        string app = ePath[0];
        

        LOG.d("Processing HTTP Call for App " + app + ": " + method + " " + path);

        if (appManager.containsEndPoint(app, path, method)) {
          try { 
            return appManager.callEndPoint(app, path, method, req);
          } catch (Exception e) {
            RestResult result = new RestResult();
            LOG.e("Exception when calling application " + app + " in endpoint " + method + " " + path + "\r\n" + e.InnerException.ToString());
            result.StatusCode = HttpStatusCode.InternalServerError;
            result.ContentType = "text/plain";
            result.Result = Encoding.UTF8.GetBytes(e.InnerException.ToString());
            return result;
          }
        } else {
          return new RestResult("No such endpoint.", "text/plain", HttpStatusCode.NotFound);
        }
      }
    }
  }
}
