using System;
using System.IO;
using System.Threading;

namespace ASTools.Logger {
  public enum LogLevel {
    ERROR = 0,
    WARN = 1,
    INFO = 2,
    DEBUG = 3
  }

  public class LogManager {

    private static readonly ConsoleColor[] logColors = new ConsoleColor[] {
      ConsoleColor.Red,       // Error
      ConsoleColor.Yellow,    // Warn
      ConsoleColor.Cyan,      // Info
      ConsoleColor.DarkGray   // Debug
    };

    private static LogManager instance;

    public static void initialize (string logFolder, LogLevel logLevel) {
      LogManager.instance = new LogManager (logFolder, logLevel);
    }

    public static LogManager Instance {
      get { return instance; }
    }

    private string logFolder;
    private LogLevel logLevel;

    private LogManager (string logFolder, LogLevel logLevel) {
      this.logFolder = logFolder;
      this.logLevel = logLevel;
    }

    private FileStream waitForFile (string fullPath, FileMode mode) {
      for (int numTries = 0; numTries < 10; numTries++) {
        try {
          return new FileStream(fullPath, mode);
        } catch (IOException) {
          Thread.Sleep (50);
        }
      }

      return null;
    }

    public async void WriteLog (string className, LogLevel level, string strLog) {
      if (level <= this.logLevel) {
        StreamWriter log;
        FileStream fileStream = null;
        DirectoryInfo logDirInfo = null;
        FileInfo logFileInfo;

        string logFilePath = Path.Combine (this.logFolder, "log-" + System.DateTime.Today.ToString ("MM-dd-yyyy") + "-" + AppDomain.CurrentDomain.FriendlyName + "." + "txt");
        logFileInfo = new FileInfo (logFilePath);
        logDirInfo = new DirectoryInfo (logFileInfo.DirectoryName);

        if (!logDirInfo.Exists)
          logDirInfo.Create ();
        if (!logFileInfo.Exists) {
          fileStream = logFileInfo.Create ();
        } else {
          fileStream = waitForFile (logFilePath, FileMode.Append);
        }

        log = new StreamWriter (fileStream);
        log.WriteLine (TextTools.formatToLog (DateTime.Now.ToString (), className, level.ToString (), strLog));
        log.Close ();

        Console.ForegroundColor = logColors [(int)level];
        Console.WriteLine (TextTools.formatToLog (DateTime.Now.ToString (), className, level.ToString (), strLog));
        Console.ResetColor ();
      }
    }
  }
}
