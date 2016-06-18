using AppServer.Server;
using ASTools.Logger;
using System.IO;
using System.Xml.Serialization;

namespace AppServer {
  class Program {

    static void Main(string[] args) {

      ServerConfig settings = new ServerConfig();
      try {
        string path = "AppConfig.xml";
        XmlSerializer x = new XmlSerializer(typeof(ServerConfig));
        StreamReader reader = new StreamReader(path);
        settings = (ServerConfig)x.Deserialize(reader);
      } catch {
        // Do nothing for now
      }

      LogManager.initialize(settings.LogFolder, settings.LogLevel);
      ASRunner runner = new ASRunner(settings.ListenURL);
      runner.run();
    }

  }
}
