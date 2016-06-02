using ASAttrib.Attributes;
using ASAttrib.Models;
using System;

namespace SampleApp {
  [REST("/hue")]
  public class MyRestSample {

    [GET("/test")]
    public string hueTest([QueryParam] string param0, [QueryParam] float param1) {
      return "GET TO HUEHUE with param: Param0(" + param0 + "), Param1(" + param1 +")";
    }

    [POST("/test")]
    public TestModel hueTest2(TestModel model) {
      model.count += 100;
      return model;
    }

    [GET("/exception-test")]
    public TestModel exceptionTest() {
      throw new NullReferenceException("Test of an Exception");
    }

    [GET("/custom-exception-test")]
    public TestModel customExceptionTest() {
      throw new CustomException("NOOOOOOOOOOOOOOOOOOOOO!");
    }
  }
}
