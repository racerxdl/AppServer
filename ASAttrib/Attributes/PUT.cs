using System;
namespace ASAttrib.Attributes {
  [AttributeUsage(AttributeTargets.Method)]
  public class PUT : Attribute, HTTPMethod {
    private string path;
    public PUT(string path) {
      this.path = path;
    }

    public string Method {
      get { return "PUT"; }
    }

    public string Path {
      get { return path; }
    }
  }
}
