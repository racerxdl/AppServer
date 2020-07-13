using System;

namespace SharpBoss.Attributes {
  /// <summary>
  /// Define expected request path parameter
  /// </summary>
  [AttributeUsage (AttributeTargets.Parameter)]
  public class PathParam : Attribute {
    private string _paramName;

    /// <summary>
    /// Create new request path parameter without name
    /// </summary>
    public PathParam () : this (null) { }

    /// <summary>
    /// Create new request path parameter with name
    /// </summary>
    /// <param name="paramName">Query string parameter name</param>
    public PathParam (string paramName) {
      this._paramName = paramName;
    }

    /// <summary>
    /// Retrieve parameter name from query string
    /// </summary>
    public string ParamName {
      get { return _paramName; }
    }
  }
}