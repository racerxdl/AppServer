using ASAttrib.Attributes;
using ASAttrib.Models;
using System;

namespace SampleApp {
  [REST("/hue")]
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
      throw new NullReferenceException("Test of an Exception");
    }

    [GET("/custom-exception-test")]
    public TestModel customExceptionTest(RestRequest request) {
      throw new CustomException("NOOOOOOOOOOOOOOOOOOOOO!");
    }
  }
}
