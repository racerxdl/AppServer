namespace SharpBoss {
  /// <summary>
  /// Interface to implement HTTP Method for RESTful Attributes
  /// </summary>
  internal interface IHTTPMethod {
    string Path { get; }
    string Method { get; }
  }
}
