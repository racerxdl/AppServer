using System;

namespace SharpBoss.Attributes {
  /// <summary>
  /// Attriute to define injectable route
  /// </summary>
  [AttributeUsage (AttributeTargets.Field)]
  public class Inject : Attribute { }
}
