using System;

namespace ASAttrib.Attributes {
  [AttributeUsage(AttributeTargets.Parameter)]
  public class QueryParam: Attribute {
    private string paramName;
    public QueryParam() {

    }

    public QueryParam(string paramName) {
      this.paramName = paramName;
    }

    public string ParamName {
      get { return paramName; }
    }
  }
}
