using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SharpBoss.Processors {
  /// <summary>
  /// Utils to process REST requests
  /// </summary>
  internal class Tools {
    /// <summary>
    /// Retrieve a list for types from namespace
    /// </summary>
    /// <param name="assembly">Assembly</param>
    /// <param name="nameSpace">base Namespace</param>
    /// <returns>A list for types</returns>
    public static List<Type> GetTypesInNamespace (Assembly assembly, string nameSpace) {
      return assembly.GetTypes ()
            .Where (t => String.Equals (t.Namespace, nameSpace, StringComparison.Ordinal))
            .ToList ();
    }
  }
}
