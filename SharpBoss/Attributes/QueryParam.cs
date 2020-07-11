using System;

namespace SharpBoss.Attributes {
  /// <summary>
  /// Define expected query string parameter
  /// </summary>
  [AttributeUsage (AttributeTargets.Parameter)]
  public class QueryParam : Attribute {
    private string paramName;

    /// <summary>
    /// Create new query string parameter without name
    /// </summary>
    public QueryParam () : this (null) { }

    /// <summary>
    /// Create new query string parameter with name
    /// </summary>
    /// <param name="paramName">Query string parameter name</param>
    public QueryParam (string paramName) {
      this.paramName = paramName;
    }

    /// <summary>
    /// Retrieve parameter name from query string
    /// </summary>
    public string ParamName {
      get { return paramName; }
    }
  }
}
