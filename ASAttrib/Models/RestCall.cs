using ASAttrib.Attributes;
using ASAttrib.Proxy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ASAttrib.Models {
  internal class RestCall {
    public string className;
    public string methodName;
    public HTTPMethod method;
    public REST baseRest;
  }
}
