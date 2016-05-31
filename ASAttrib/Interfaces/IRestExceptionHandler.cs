using ASAttrib.Models;
using System;

namespace ASAttrib {
  public interface IRestExceptionHandler {
    RestResult handleException(Exception e);
  }
}
