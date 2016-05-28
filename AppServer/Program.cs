using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using ASAttrib.Attributes;
using System.Net;
using AppServer.Server;
using ASAttrib.Processors;
using ASTools.Logger;

namespace AppServer {
  class Program {

    static void Main(string[] args) {
      LogManager.initialize(".\\", LogLevel.DEBUG);
      ASRunner runner = new ASRunner();
      runner.run();
    }

  }
}
