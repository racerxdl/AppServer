using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampleApp {
  public class TestProc {

    public string myName() {
      return typeof(TestProc).Name;
    }

    public TestModel addCount(TestModel t, int c) {
      t.count += c;
      return t;
    }
  }
}
