using System;
using System.IO;

namespace SharpBoss {
  /// <summary>
  /// Types of supported platforms
  /// </summary>
  public enum Platform {
    Windows,
    Linux,
    Mac
  }

  /// <summary>
  /// Utilities and tools
  /// </summary>
  public class Utils {
    public static readonly char DIRECTORY_SEPARATOR = GetRunningPlatform () == Platform.Windows ? '\\' : '/';
    public static readonly string DEBUG_SYMBOLS_EXTENSION = GetRunningPlatform () == Platform.Windows ? ".pdb" : ".dll.mdb";

    /// <summary>
    /// Retrieve current platform which application is running
    /// </summary>
    /// <returns>Platform type</returns>
    public static Platform GetRunningPlatform () {
      switch (Environment.OSVersion.Platform) {
        case PlatformID.Unix:
          if (Directory.Exists ("/Applications") & Directory.Exists ("/System") & Directory.Exists ("/Users") & Directory.Exists ("/Volumes")) {
            return Platform.Mac;
          } else {
            return Platform.Linux;
          }
        case PlatformID.MacOSX:
          return Platform.Mac;

        default:
          return Platform.Windows;
      }
    }
  }
}

