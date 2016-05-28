using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASTools.Logger {
  public class Logger {

    private string className;

    public Logger(Type classType) {
      this.className = classType.FullName;
    }

    public void d(string logMessage) {
      LogManager.Instance.WriteLog(className, LogLevel.DEBUG, logMessage);
    }
    public void i(string logMessage) {
      LogManager.Instance.WriteLog(className, LogLevel.INFO, logMessage);
    }
    public void w(string logMessage) {
      LogManager.Instance.WriteLog(className, LogLevel.WARN, logMessage);
    }
    public void e(string logMessage) {
      LogManager.Instance.WriteLog(className, LogLevel.ERROR, logMessage);
    }
  }
}
