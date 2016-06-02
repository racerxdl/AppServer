using System;

namespace ASAttrib.Attributes {
  [AttributeUsage(AttributeTargets.Parameter)]
  public class PathParam : Attribute {
    private string paramName;
    public PathParam() {

    }

    public PathParam(string paramName) {
      this.paramName = paramName;
    }

    public string ParamName {
      get { return paramName; }
    }
  }
}
