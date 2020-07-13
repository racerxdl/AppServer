using System;

namespace SharpBoss.Attributes.Methods {
  /// <summary>
  /// Attribute for RESTful HTTP GET Method
  /// </summary>
  [AttributeUsage (AttributeTargets.Method)]
  public class GET : Attribute, IHTTPMethod {
    private string _path;

    /// <summary>
    /// Create new route with path for GET request
    /// </summary>
    /// <param name="path">Request path</param>
    public GET (string path) {
      this._path = path;
    }

    /// <summary>
    /// Retrieve HTTP Method
    /// </summary>
    public string Method {
      get { return "GET"; }
    }

    /// <summary>
    /// Retrieve Request path
    /// </summary>
    public string Path {
      get { return _path; }
    }
  }
}
