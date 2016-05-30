using ASAttrib.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ASAttrib.Models {
  public class RestCall {
    public Type methodClass;
    public MethodInfo call;
    public HTTPMethod method;
    public Rest baseRest;
  }
}
