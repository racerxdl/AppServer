using System;

namespace SharpBoss.Attributes {
  /// <summary>
  /// Handle exceptions to a specific method
  /// </summary>
  [AttributeUsage (AttributeTargets.Class, AllowMultiple = true)]
  public class RestExceptionHandler : Attribute {
    public Type _exceptionType;

    /// <summary>
    /// Create an exception handler for type
    /// </summary>
    /// <param name="exceptionType"></param>
    public RestExceptionHandler (Type exceptionType) {
      this._exceptionType = exceptionType;
    }

    /// <summary>
    /// Retrieve type from Exception
    /// </summary>
    public Type ExceptionType {
      get { return _exceptionType; }
    }
  }
}
