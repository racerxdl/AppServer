using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASAttrib.Attributes {
  [AttributeUsage(AttributeTargets.Method)]
  public class GET : Attribute, HTTPMethod {
    private string path;
    public GET(string path) {
      this.path = path;
    }

    public string Method {
      get { return "GET"; }
    }

    public string Path {
      get { return path; }
    }
  }
}
