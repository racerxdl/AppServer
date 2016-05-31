using System;
using System.IO;

namespace ASTools {
  public enum Platform {
    Windows,
    Linux,
    Mac
  }

  public class Tools {
    public static readonly char DIRECTORY_SEPARATOR = getRunningPlatform() == Platform.Windows ? '\\' : '/';

    public static Platform getRunningPlatform() {
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

