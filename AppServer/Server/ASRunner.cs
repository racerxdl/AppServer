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
    RestProcessor processor;
    HttpServer server;

    public ASRunner() {
      server = new HttpServer("http://localhost:8080/", processHttpCalls);
      processor = new RestProcessor(Assembly.GetExecutingAssembly(), "AppServer.Modules");
      Console.WindowWidth = 180;
    }

    public void run() {
      server.Run();

      Console.ReadLine();
    }
    RestResult processHttpCalls(HttpListenerRequest request) {
      string path = request.RawUrl;
      string method = request.HttpMethod;

      if (processor.containsEndPoint(path, method)) {
        return processor.callEndPoint(path, method, request);
      } else {
        return new RestResult("No such endpoint.", "text/plain", HttpStatusCode.NotFound);
      }
    }
  }
}
