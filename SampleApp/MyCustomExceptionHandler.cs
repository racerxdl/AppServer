using ASAttrib;
using ASAttrib.Attributes;
using System;
using ASAttrib.Models;
using System.Text;

namespace SampleApp {
  [RestExceptionHandler(typeof(CustomException))]
  public class MyCustomExceptionHandler : IRestExceptionHandler {
    public RestResult handleException(Exception e) {
      CustomException ce = (CustomException)e;    //  The Custom Exception handler will only be called with the correct type of exception

      RestResult result = new RestResult();
      result.ContentType = "text/plain";
      result.StatusCode = System.Net.HttpStatusCode.NotAcceptable;
      result.Result = Encoding.UTF8.GetBytes("Handling CustomException that has a message: " + ce.Message);

      return result;
    }
  }
}
