using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EmbedIO;
using SharpBoss.Logging;
using SharpBoss.Models;
using SharpBoss.Workers;

namespace SharpBoss;

/// <summary>
/// Hosts dynamically loaded SharpBoss applications over HTTP.
/// </summary>
public sealed class SharpBoss : IDisposable, IAsyncDisposable
{
    private const string DefaultListenUrl = "http://localhost:8080/";
    private const string ListenUrlSetting = "SHARPBOSS_URL";

    private readonly WebServer _webServer;
    private readonly ApplicationLoader _applicationLoader;
    private readonly string _listenUrl;
    private readonly object _lifecycleGate = new();
    private CancellationTokenSource? _runCancellation;
    private Task? _runTask;
    private bool _hasStarted;
    private int _disposed;

    /// <summary>
    /// Creates a server. Explicit URL, environment, settings, and the default URL are checked in that order.
    /// </summary>
    public SharpBoss(
        string? listenUrl = null,
        IReadOnlyDictionary<string, string>? appSettings = null,
        SharpBossOptions? options = null)
    {
        options ??= new SharpBossOptions();
        options.Validate();

        _listenUrl = NormalizeListenUrl(GetListenUrl(listenUrl, appSettings));
        _applicationLoader = new ApplicationLoader(options);
        _webServer = new WebServer(configuration => configuration
                .WithUrlPrefix(_listenUrl)
                .WithMode(HttpListenerMode.EmbedIO))
            .WithAction(HttpVerbs.Any, RequestHandlerCallback);
    }

    /// <summary>
    /// Forces every application directory to be reloaded atomically.
    /// </summary>
    public void ForceReload()
    {
        ThrowIfDisposed();
        _applicationLoader.ForceReload();
    }

    /// <summary>
    /// Starts the server and returns its lifetime task.
    /// </summary>
    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        lock (_lifecycleGate)
        {
            if (_hasStarted)
            {
                throw new InvalidOperationException("A SharpBoss server instance can only be started once.");
            }

            _hasStarted = true;
            _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _runTask = _webServer.RunAsync(_runCancellation.Token);
            _ = _runTask.ContinueWith(
                static task => Logger.Error("The SharpBoss web server stopped unexpectedly.", task.Exception!.GetBaseException()),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
            Logger.Info($"SharpBoss is listening at {_listenUrl}.");
            return _runTask;
        }
    }

    /// <summary>
    /// Starts the server without blocking the calling thread.
    /// </summary>
    public void Run()
    {
        _ = RunAsync();
    }

    /// <summary>
    /// Stops the server and observes its lifetime task.
    /// </summary>
    public Task StopAsync()
    {
        return StopCoreAsync();
    }

    /// <summary>
    /// Stops the server synchronously.
    /// </summary>
    public void Stop()
    {
        StopCoreAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Gets the normalized HTTP listen URL.
    /// </summary>
    public string GetHttpServerListenUrl()
    {
        return _listenUrl;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await StopCoreAsync().ConfigureAwait(false);
        _webServer.Dispose();
        _applicationLoader.Dispose();
    }

    internal WeakReference? LastRetiredLoadContext => _applicationLoader.LastRetiredLoadContext;

    internal void CleanupRetiredApplications()
    {
        _applicationLoader.CleanupRetiredApplications();
    }

    private async Task RequestHandlerCallback(IHttpContext context)
    {
        RestResponse response;

        try
        {
            var request = await RestRequest.CreateAsync(context).ConfigureAwait(false);
            response = Process(request);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Logger.Error($"Request '{context.Id}' failed.", exception);
            response = new RestResponse(
                $"Internal server error. Reference: {context.Id}",
                "text/plain",
                HttpStatusCode.InternalServerError);
        }

        context.Response.StatusCode = (int)response.StatusCode;
        context.Response.ContentType = response.ContentType;
        context.Response.ContentLength64 = response.Result.Length;
        await context.Response.OutputStream
            .WriteAsync(response.Result.AsMemory(), context.CancellationToken)
            .ConfigureAwait(false);
    }

    private RestResponse Process(RestRequest request)
    {
        var endpointPath = request.Url.AbsolutePath.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
        if (endpointPath.Length == 0)
        {
            return new RestResponse("No such endpoint.", "text/plain", HttpStatusCode.NotFound);
        }

        var applicationName = endpointPath[0];
        var path = endpointPath.Length > 1 ? "/" + endpointPath[1] : "/";
        Logger.Debug($"Received request for APP={applicationName} {request.HttpMethod} {path}.");

        return _applicationLoader.TryProcess(
            applicationName,
            path,
            request.HttpMethod,
            request,
            out var response)
                ? response!
                : new RestResponse("No such endpoint.", "text/plain", HttpStatusCode.NotFound);
    }

    private async Task StopCoreAsync()
    {
        Task? runTask;
        CancellationTokenSource? cancellation;

        lock (_lifecycleGate)
        {
            runTask = _runTask;
            cancellation = _runCancellation;
            cancellation?.Cancel();
        }

        if (runTask is not null)
        {
            try
            {
                await runTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellation?.IsCancellationRequested == true)
            {
            }
        }

        lock (_lifecycleGate)
        {
            if (ReferenceEquals(runTask, _runTask))
            {
                _runTask = null;
                _runCancellation = null;
                cancellation?.Dispose();
            }
        }
    }

    private static string GetListenUrl(
        string? explicitListenUrl,
        IReadOnlyDictionary<string, string>? appSettings)
    {
        if (explicitListenUrl is not null)
        {
            return explicitListenUrl;
        }

        var environmentValue = Environment.GetEnvironmentVariable(ListenUrlSetting);
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return environmentValue;
        }

        if (appSettings is not null
            && appSettings.TryGetValue(ListenUrlSetting, out var configuredValue)
            && !string.IsNullOrWhiteSpace(configuredValue))
        {
            return configuredValue;
        }

        return DefaultListenUrl;
    }

    private static string NormalizeListenUrl(string listenUrl)
    {
        if (!Uri.TryCreate(listenUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException($"'{listenUrl}' is not a valid absolute HTTP URL.", nameof(listenUrl));
        }

        return listenUrl.EndsWith("/", StringComparison.Ordinal) ? listenUrl : listenUrl + "/";
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    }
}
