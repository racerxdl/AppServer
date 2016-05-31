using System;

namespace SampleApp {
  public class CustomException: Exception {
    public CustomException(string message) : base(message) {

    }
  }
}
