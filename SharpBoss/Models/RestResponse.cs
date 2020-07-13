using System;
using System.Net;
using System.Text;

namespace SharpBoss.Models {
  /// <summary>
  /// The Boss response class
  /// </summary>
  [Serializable]
  public class RestResponse {
    private HttpStatusCode _statusCode;
    private string _contentType;
    private byte[] _result;

    /// <summary>
    /// Returns status code from response
    /// </summary>
    public HttpStatusCode StatusCode {
      get { return _statusCode; }
      set { _statusCode = value; }
    }

    /// <summary>
    /// Returns content type from response
    /// </summary>
    public string ContentType {
      get { return _contentType; }
      set { _contentType = value; }
    }

    /// <summary>
    /// Returns status code from response
    /// </summary>
    public byte[] Result {
      get { return _result; }
      set { _result = value; }
    }

    /// <summary>
    /// Instantiate new Rest response with default data
    /// </summary>
    public RestResponse () {
      this._statusCode = HttpStatusCode.OK;
      this._contentType = "text/plain";
      this._result = new byte[0];
    }

    /// <summary>
    /// Instantiate new Rest response with result
    /// </summary>
    /// <param name="result">Result from request</param>
    public RestResponse (string result) : this () {
      this._result = Encoding.UTF8.GetBytes (result);
    }

    /// <summary>
    /// Instantiate new Rest response with result and content type
    /// </summary>
    /// <param name="result">Result from request</param>
    /// <param name="contentType">Content type from request</param>
    public RestResponse (string result, string contentType) : this () {
      this._result = Encoding.UTF8.GetBytes (result);
      this._contentType = contentType;
    }

    /// <summary>
    /// Instantiate new Rest response with result and content type
    /// </summary>
    /// <param name="result">Result from request</param>
    /// <param name="contentType">Content type from request</param>
    public RestResponse (byte[] result, string contentType) : this () {
      this._result = result;
      this._contentType = contentType;
    }

    /// <summary>
    /// Instantiate new Rest response with result and content type
    /// </summary>
    /// <param name="result">Result from request</param>
    /// <param name="contentType">Content type from request</param>
    /// <param name="statusCode">Status code from request</param>
    public RestResponse (string result, string contentType, HttpStatusCode statusCode) {
      this._result = Encoding.UTF8.GetBytes (result);
      this._contentType = contentType;
      this._statusCode = statusCode;
    }

    /// <summary>
    /// Instantiate new Rest response with result and content type
    /// </summary>
    /// <param name="result">Result from request</param>
    /// <param name="contentType">Content type from request</param>
    /// <param name="statusCode">Status code from request</param>
    public RestResponse (byte[] result, string contentType, HttpStatusCode statusCode) {
      this._result = result;
      this._contentType = contentType;
      this._statusCode = statusCode;
    }
  }
}
