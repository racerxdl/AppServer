using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using EmbedIO;

namespace SharpBoss.Models;

/// <summary>
/// Immutable request data exposed to REST endpoints.
/// </summary>
public sealed class RestRequest
{
    private RestRequest(IHttpRequest request, NameValueCollection headers, string? body)
    {
        Headers = headers;
        AcceptTypes = ParseHeaderList(headers["Accept"]);
        ContentEncoding = request.ContentEncoding;
        ContentLength = request.ContentLength64;
        ContentType = request.ContentType;
        Cookies = ParseCookies(request.Url, headers["Cookie"]);
        HttpMethod = request.HttpMethod;
        IsAuthenticated = request.IsAuthenticated;
        IsLocal = request.IsLocal;
        IsSecureConnection = request.IsSecureConnection;
        IsWebSocketRequest = request.IsWebSocketRequest;
        KeepAlive = request.KeepAlive;
        QueryString = new NameValueCollection(request.QueryString);
        RawUrl = request.RawUrl;
        UserAgent = request.UserAgent;
        UserHostAddress = request.RemoteEndPoint?.ToString() ?? string.Empty;
        UserHostName = headers["Host"] ?? request.LocalEndPoint?.ToString() ?? string.Empty;
        UserLanguages = ParseHeaderList(headers["Accept-Language"]);
        Body = body;
        Url = request.Url;
        UrlReferrer = request.UrlReferrer;
    }

    public string[] AcceptTypes { get; }

    public Encoding ContentEncoding { get; }

    public long ContentLength { get; }

    public string? ContentType { get; }

    public CookieCollection Cookies { get; }

    public NameValueCollection Headers { get; }

    public string HttpMethod { get; }

    public bool IsAuthenticated { get; }

    public bool IsLocal { get; }

    public bool IsSecureConnection { get; }

    public bool IsWebSocketRequest { get; }

    public bool KeepAlive { get; }

    public NameValueCollection QueryString { get; }

    public string RawUrl { get; }

    public string UserAgent { get; }

    public string UserHostAddress { get; }

    public string UserHostName { get; }

    public string[] UserLanguages { get; }

    public string? Body { get; }

    public Uri Url { get; }

    public Uri? UrlReferrer { get; }

    internal static async ValueTask<RestRequest> CreateAsync(IHttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var request = context.Request;
        string? body = null;

        if (request.HasEntityBody)
        {
            using var reader = new StreamReader(
                request.InputStream,
                request.ContentEncoding,
                detectEncodingFromByteOrderMarks: true,
                leaveOpen: true);
            body = await reader.ReadToEndAsync(context.CancellationToken).ConfigureAwait(false);
        }

        return new RestRequest(request, new NameValueCollection(request.Headers), body);
    }

    private static string[] ParseHeaderList(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static CookieCollection ParseCookies(Uri requestUrl, string? cookieHeader)
    {
        if (string.IsNullOrWhiteSpace(cookieHeader))
        {
            return new CookieCollection();
        }

        try
        {
            var container = new CookieContainer();
            container.SetCookies(requestUrl, cookieHeader);
            return container.GetCookies(requestUrl);
        }
        catch (CookieException)
        {
            return new CookieCollection();
        }
    }
}
