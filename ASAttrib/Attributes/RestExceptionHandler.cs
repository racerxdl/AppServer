using ASAttrib.Exceptions;
using System;

namespace ASAttrib.Attributes {
  [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
  public class RestExceptionHandler : Attribute {
    public Type exceptionType;

    public RestExceptionHandler(Type exceptionType) {
      this.exceptionType = exceptionType;
    }

    public Type ExceptionType {
      get { return exceptionType; }
    }
  }
}
