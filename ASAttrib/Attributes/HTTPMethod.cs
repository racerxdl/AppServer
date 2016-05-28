using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASAttrib.Attributes {
  public interface HTTPMethod {

    string Path {
      get;
    }
    string Method {
      get;
    }
  }
}
