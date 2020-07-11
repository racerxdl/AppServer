using System;

namespace SharpBoss.Attributes.Methods {
  /// <summary>
  /// Attribute for RESTful HTTP DELETE Method
  /// </summary>
  [AttributeUsage(AttributeTargets.Method)]
  public class DELETE : Attribute, IHTTPMethod {
    private string _path;

    /// <summary>
    /// Create new route with path for DELETE request
    /// </summary>
    /// <param name="path">Request path</param>
    public DELETE(string path) {
      this._path = path;
    }

    /// <summary>
    /// Retrieve HTTP Method
    /// </summary>
    public string Method {
      get { return "DELETE"; }
    }

    /// <summary>
    /// Retrieve Request path
    /// </summary>
    public string Path {
      get { return _path; }
    }
  }
}
