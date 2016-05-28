using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    private static readonly string logFormat = "{0,-19} - {1, -40} - {2, -6}: {3}";

    private static LogManager instance;

    public static void initialize(string logFolder, LogLevel logLevel) {
      LogManager.instance = new LogManager(logFolder, logLevel);
    }

    public static LogManager Instance {
      get { return instance; }
    }

    private string logFolder;
    private LogLevel logLevel;
    private LogManager(string logFolder, LogLevel logLevel) {
      this.logFolder = logFolder;
      this.logLevel = logLevel;
    }

    public void WriteLog(string className, LogLevel level, string strLog) {
      if (level <= this.logLevel) {
        StreamWriter log;
        FileStream fileStream = null;
        DirectoryInfo logDirInfo = null;
        FileInfo logFileInfo;

        string logFilePath = this.logFolder + "Log-" + System.DateTime.Today.ToString("MM-dd-yyyy") + "-" + AppDomain.CurrentDomain.FriendlyName + "." + "txt";
        logFileInfo = new FileInfo(logFilePath);
        logDirInfo = new DirectoryInfo(logFileInfo.DirectoryName);
        if (!logDirInfo.Exists)
          logDirInfo.Create();
        if (!logFileInfo.Exists) {
          fileStream = logFileInfo.Create();
        } else {
          fileStream = new FileStream(logFilePath, FileMode.Append);
        }
        log = new StreamWriter(fileStream);
        log.WriteLine(String.Format(logFormat, DateTime.Now.ToString(), className, level.ToString(), strLog));
        log.Close();

        Console.ForegroundColor = logColors[(int)level];
        Console.WriteLine(String.Format(logFormat, DateTime.Now.ToString(), className, level.ToString(), strLog));
        Console.ResetColor();
      }
    }
  }
}
