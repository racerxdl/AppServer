using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;

namespace SharpBoss.Models {
  /// <summary>
  /// The Boss request class
  /// </summary>
  [Serializable]
  public class RestRequest {
    private string[] _acceptTypes;
    private Encoding _contentEncoding;
    private long _contentLength;
    private string _contentType;
    private CookieCollection _cookies;
    private string _httpMethod;
    private bool _isAuthenticated;
    private bool _isLocal;
    private bool _isSecureConnection;
    private bool _isWebSocketRequest;
    private bool _keepAlive;
    private NameValueCollection _queryString;
    private string _rawUrl;
    private string _userAgent;
    private string _userHostAddress;
    private string _userHostName;
    private string[] _userLanguages;
    private string _body;
    private Uri _url;
    private Uri _urlReferrer;

    /// <summary>
    /// Gets the MIME types accepted by the client.
    /// </summary>
    public string[] AcceptTypes { get { return this._acceptTypes; } }

    /// <summary>
    /// Gets the content encoding that can be used with data sent with the request
    /// </summary>
    public Encoding ContentEncoding { get { return this._contentEncoding; } }

    /// <summary>
    /// Gets the length of the body data included in the request.
    /// </summary>
    public long ContentLength { get { return this._contentLength; } }

    /// <summary>
    /// Gets the MIME type of the body data included in the request.
    /// </summary>
    public string ContentType { get { return this._contentType; } }

    /// <summary>
    /// Gets the cookies sent with the request.
    /// </summary>
    public CookieCollection Cookies { get { return this._cookies; } }

    /// <summary>
    /// Gets the HTTP method specified by the client.
    /// </summary>
    public string HttpMethod { get { return this._httpMethod; } }

    /// <summary>
    /// Gets a System.Boolean value that indicates whether the client sending this request is authenticated.
    /// </summary>
    public bool IsAuthenticated { get { return this._isAuthenticated; } }

    /// <summary>
    /// Gets a System.Boolean value that indicates whether the request is sent from the local computer.
    /// </summary>
    public bool IsLocal { get { return this._isLocal; } }

    /// <summary>
    /// Gets a System.Boolean value that indicates whether the TCP connection used to send the request is using the Secure Sockets Layer (SSL) protocol.
    /// </summary>
    public bool IsSecureConnection { get { return this._isSecureConnection; } }

    /// <summary>
    /// Gets a System.Boolean value that indicates whether the TCP connection was a WebSocket request.
    /// </summary>
    public bool IsWebSocketRequest { get { return this._isWebSocketRequest; } }

    /// <summary>
    /// Gets a System.Boolean value that indicates whether the client requests a persistent connection.
    /// </summary>
    public bool KeepAlive { get { return this._keepAlive; } }

    /// <summary>
    /// Gets the query string included in the request.
    /// </summary>
    public NameValueCollection QueryString { get { return this._queryString; } }

    /// <summary>
    /// Gets the URL information (without the host and port) requested by the client.
    /// </summary>
    public string RawUrl { get { return this._rawUrl; } }

    /// <summary>
    /// Gets the user agent presented by the client.
    /// </summary>
    public string UserAgent { get { return this._userAgent; } }

    /// <summary>
    /// Gets the server IP address and port number to which the request is directed.
    /// </summary>
    public string UserHostAddress { get { return this._userHostAddress; } }

    /// <summary>
    /// Gets the DNS name and, if provided, the port number specified by the client.
    /// </summary>
    public string UserHostName { get { return this._userHostName; } }

    /// <summary>
    /// Gets the natural languages that are preferred for the response.
    /// </summary>
    public string[] UserLanguages { get { return this._userLanguages; } }

    /// <summary>
    /// Gets the body injected to request
    /// </summary>
    public string Body { get { return this._body; } }

    /// <summary>
    /// Gets the System.Uri object requested by the client.
    /// </summary>
    public Uri Url { get { return this._url; } }

    /// <summary>
    /// Gets the Uniform Resource Identifier (URI) of the resource that referred the client to the server.
    /// </summary>
    public Uri UrlReferrer { get { return this._urlReferrer; } }

    /// <summary>
    /// Instantiate a new request with listener
    /// </summary>
    /// <param name="request">Http Listener</param>
    public RestRequest (HttpListenerRequest request) {
      this._userAgent = request.UserAgent;
      this._userHostAddress = request.UserHostAddress;
      this._userHostName = request.UserHostName;
      this._userLanguages = request.UserLanguages;
      this._url = request.Url;
      this._urlReferrer = request.UrlReferrer;
      this._acceptTypes = request.AcceptTypes;
      this._contentEncoding = request.ContentEncoding;
      this._contentLength = request.ContentLength64;
      this._contentType = request.ContentType;
      this._cookies = request.Cookies;
      this._httpMethod = request.HttpMethod;
      this._isAuthenticated = request.IsAuthenticated;
      this._isLocal = request.IsLocal;
      this._isSecureConnection = request.IsSecureConnection;
      this._isWebSocketRequest = request.IsWebSocketRequest;
      this._keepAlive = request.KeepAlive;
      this._queryString = request.QueryString;
      this._rawUrl = request.RawUrl;

      if (request.HasEntityBody) {
        using (var body = request.InputStream) {
          using (var reader = new StreamReader (body, request.ContentEncoding)) {
            this._body = reader.ReadToEnd ();
          }
        }
      }
    }
  }
}
