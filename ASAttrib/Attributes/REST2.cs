using System;

namespace ASAttrib.Attributes {
  [AttributeUsage(AttributeTargets.Class) ]
  public class REST : Attribute {
    private string path;
    public REST(string path) {
      this.path = path;
    }

    public string Path {
      get { return path; }
    }
  }
}
