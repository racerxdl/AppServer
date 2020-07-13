using System;

namespace SharpBoss.Attributes.Methods {
  /// <summary>
  /// Attribute for RESTful HTTP POST Method
  /// </summary>
  [AttributeUsage (AttributeTargets.Method)]
  public class POST : Attribute, IHTTPMethod {
    private string _path;

    /// <summary>
    /// Create new route with path for POST request
    /// </summary>
    /// <param name="path">Request path</param>
    public POST (string path) {
      this._path = path;
    }

    /// <summary>
    /// Retrieve HTTP Method
    /// </summary>
    public string Method {
      get { return "POST"; }
    }

    /// <summary>
    /// Retrieve Request path
    /// </summary>
    public string Path {
      get { return _path; }
    }
  }
}
