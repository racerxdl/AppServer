using ASTools.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppServer.Server {
  public class ServerConfig {
    private string listenURL = "http://localhost:8080/";
    private LogLevel logLevel = LogLevel.DEBUG;
    private string logFolder = ".";

    #region Properties

    /// <summary>
    /// get/set the ListenURL
    /// </summary>
    public string ListenURL {
      get { return listenURL; }
      set { listenURL = value; }
    }

    /// <summary>
    /// get/set the LogLevel (String)
    /// </summary>
    public string LogLevelS {
      get { return logLevel.ToString(); }
      set {
        try {
          logLevel = (LogLevel)Enum.Parse(typeof(LogLevel), value);
        } catch {
          logLevel = LogLevel.DEBUG;
        }
      }
    }

    public LogLevel LogLevel {
      get { return logLevel; }
      set { logLevel = value; }
    }

    public string LogFolder {
      get { return logFolder; }
      set { logFolder = value; }
    }

    #endregion

    public ServerConfig() {

    }
  }
}
