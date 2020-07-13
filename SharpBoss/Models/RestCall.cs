using SharpBoss;
using SharpBoss.Attributes;

namespace SharpBoss.Models {
  /// <summary>
  /// REST Call struct
  /// </summary>
  internal class RestCall {
    private string _className;
    private string _methodName;
    private IHTTPMethod _method;
    private REST _restClass;

    public RestCall (string className, string methodName, IHTTPMethod method, REST restClass) {
      this._className = className;
      this._methodName = methodName;
      this._method = method;
      this._restClass = restClass;
    }

    /// <summary>
    /// Retrieve REST Class name
    /// </summary>
    public string ClassName {
      get { return this._className; }
    }

    /// <summary>
    /// Retrieve method name from REST Class
    /// </summary>
    public string MethodName {
      get { return this._methodName; }
    }

    /// <summary>
    /// Retrieve http method name from method
    /// </summary>
    public IHTTPMethod HttpMethod {
      get { return this._method; }
    }

    /// <summary>
    /// Retrieve REST Class attribute
    /// </summary>
    public REST RestClass {
      get { return this._restClass; }
    }
  }
}
