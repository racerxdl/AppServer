using ASAttrib.Attributes;
using ASAttrib.Models;
using System;
using System.Net;

namespace SampleApp {
  [Rest("/hue")]
  public class MyRestSample {

    [GET("/test")]
    public string hueTest(RestRequest request) {
      return "GET TO HUEHUE";
    }

    [POST("/test")]
    public TestModel hueTest2(RestRequest request) {
      TestModel x = new TestModel();
      x.name = "Lucas";
      x.count = 10;
      x.test = "HUEHUE";
      return x;
    }

    [GET("/exception-test")]
    public TestModel exceptionTest(RestRequest request) {
      TestModel x;

      throw new NullReferenceException("Test of an Exception");
    }
  }
}
