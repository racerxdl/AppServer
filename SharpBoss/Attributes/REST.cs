using System;

namespace SharpBoss.Attributes {
  /// <summary>
  /// Attribute to handle routes from REST class
  /// </summary>
  [AttributeUsage (AttributeTargets.Class)]
  public class REST : Attribute {
    private string _basePath;

    /// <summary>
    /// Create new REST scope for defined base path
    /// </summary>
    /// <param name="path"></param>
    public REST (string basePath) {
      this._basePath = basePath;
    }

    /// <summary>
    /// Retrieve base path for REST class
    /// </summary>
    public string Path {
      get { return _basePath; }
    }
  }
}
