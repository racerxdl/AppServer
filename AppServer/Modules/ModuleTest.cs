using AppServer.Models;
using ASAttrib.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AppServer.Modules {
  [Rest("/hue")]
  public class ModuleTest {

    [GET("/test")]
    public string hueTest(HttpListenerRequest request) {
      return "GET TO HUEHUE";
    }

    [POST("/test")]
    public TestModel hueTest2(HttpListenerRequest request) {
      TestModel x = new TestModel();
      x.name = "Lucas";
      x.count = 10;
      x.test = "HUEHUE";
      return x;
    }

    [POST("/test")]
    public TestModel hueTest3(HttpListenerRequest request) {
      TestModel x = new TestModel();
      x.name = "Lucas";
      x.count = 10;
      x.test = "HUEHUE";
      return x;
    }
  }
}
