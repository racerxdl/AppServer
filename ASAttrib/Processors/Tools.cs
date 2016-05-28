using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ASAttrib.Processors {
  internal class Tools {
    public static Type[] GetTypesInNamespace(Assembly assembly, string nameSpace) {
      return assembly.GetTypes().Where(t => String.Equals(t.Namespace, nameSpace, StringComparison.Ordinal)).ToArray();
    }
  }
}
