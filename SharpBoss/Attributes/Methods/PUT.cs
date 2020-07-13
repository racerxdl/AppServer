using System;

namespace SharpBoss.Attributes.Methods {
  /// <summary>
  /// Attribute for RESTful HTTP PUT Method
  /// </summary>
  [AttributeUsage (AttributeTargets.Method)]
  public class PUT : Attribute, IHTTPMethod {
    private string _path;

    /// <summary>
    /// Create new route with path for PUT request
    /// </summary>
    /// <param name="path">Request path</param>
    public PUT (string path) {
      this._path = path;
    }

    /// <summary>
    /// Retrieve HTTP Method
    /// </summary>
    public string Method {
      get { return "PUT"; }
    }

    /// <summary>
    /// Retrieve Request path
    /// </summary>
    public string Path {
      get { return _path; }
    }
  }
}
