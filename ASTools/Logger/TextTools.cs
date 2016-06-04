using System;

namespace ASTools.Logger {
  internal class TextTools {
    private static readonly string logFormat = "{0,-19} - {1, -40} - {2, -6}: {3}";
    private static readonly string logFormatNewLine = "                                                                         ";

    public static string formatToLog(string dateTime, string className, string level, string logData) {
      string ret = "";

      string[] lines = logData.Split('\n');
      for (int i = 1; i < lines.Length; i++) {
        lines[i] = logFormatNewLine + lines[i];
      }

      ret = String.Join("\n", lines);
      ret = String.Format(logFormat, dateTime, className, level, ret);
      return ret;
    }
  }
}
