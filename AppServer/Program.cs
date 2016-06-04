using AppServer.Server;
using ASTools.Logger;

namespace AppServer {
  class Program {

    static void Main(string[] args) {
      LogManager.initialize(".", LogLevel.DEBUG);
      ASRunner runner = new ASRunner();
      runner.run();
    }

  }
}
