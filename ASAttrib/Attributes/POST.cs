using System;

namespace ASAttrib.Attributes {
  [AttributeUsage(AttributeTargets.Method)]
  public class POST : Attribute, HTTPMethod {
    private string path;
    public POST(string path) {
      this.path = path;
    }

    public string Method {
      get { return "POST"; }
    }

    public string Path {
      get { return path; }
    }
  }
}
