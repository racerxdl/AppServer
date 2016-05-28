using System;

namespace ASAttrib.Attributes {
  [AttributeUsage(AttributeTargets.Class) ]
  public class Rest : Attribute {
    private string path;
    public Rest(string path) {
      this.path = path;
    }

    public string Path {
      get { return path; }
    }
  }
}
