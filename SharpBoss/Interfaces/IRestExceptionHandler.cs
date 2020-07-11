using SharpBoss.Models;
using System;

namespace SharpBoss {
  /// <summary>
  /// Interface for custom Rest Exception handler
  /// </summary>
  public interface IRestExceptionHandler {
    RestResponse HandleException(Exception e);
  }
}