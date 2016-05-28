using System;

namespace ASAttrib.Attributes {
  [AttributeUsage(AttributeTargets.Method)]
  public class DELETE : Attribute, HTTPMethod {
    private string path;
    public DELETE(string path) {
      this.path = path;
    }

    public string Method {
      get { return "DELETE"; }
    }

    public string Path {
      get { return path; }
    }
  }
}
