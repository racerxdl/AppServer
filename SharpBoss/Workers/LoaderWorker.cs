using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using SharpBoss.Logging;
using SharpBoss.Models;
using SharpBoss.Processors;
using SharpBoss.Runtime;

namespace SharpBoss.Workers;

/// <summary>
/// Owns endpoint state for one loaded application generation.
/// </summary>
public sealed class LoaderWorker : IDisposable
{
    private RestProcessor? _restProcessor;

    /// <summary>
    /// Creates a worker with default JSON serialization settings.
    /// </summary>
    public LoaderWorker()
    {
        _restProcessor = new RestProcessor();
        Logger.Info("LoaderWorker created.");
    }

    internal LoaderWorker(IApplicationJsonSerializer jsonSerializer)
    {
        ArgumentNullException.ThrowIfNull(jsonSerializer);
        _restProcessor = new RestProcessor(jsonSerializer);
        Logger.Info("LoaderWorker created.");
    }

    /// <summary>
    /// Registers endpoints and application services from an assembly.
    /// </summary>
    public void LoadAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        Logger.Info($"Loading {assembly.FullName}");
        GetProcessor().Init(assembly);
    }

    /// <summary>
    /// Gets whether the worker contains an endpoint.
    /// </summary>
    public bool ContainsEndPoint(string path, string method)
    {
        return GetProcessor().ContainsEndPoint(path, method);
    }

    /// <summary>
    /// Processes an endpoint request.
    /// </summary>
    public RestResponse Process(string path, string method, RestRequest request)
    {
        var processor = GetProcessor();

        try
        {
            return processor.Process(path, method, request);
        }
        catch (Exception exception)
        {
            var applicationException = exception is TargetInvocationException { InnerException: not null }
                ? exception.InnerException
                : exception;
            var handler = processor.GetExceptionHandler(applicationException.GetType());

            if (handler is not null)
            {
                return handler.HandleException(applicationException);
            }

            ExceptionDispatchInfo.Capture(applicationException).Throw();
            throw new UnreachableException();
        }
    }

    internal int EndpointCount => GetProcessor().EndpointCount;

    /// <inheritdoc />
    public void Dispose()
    {
        Interlocked.Exchange(ref _restProcessor, null)?.Dispose();
    }

    private RestProcessor GetProcessor()
    {
        return Volatile.Read(ref _restProcessor)
            ?? throw new ObjectDisposedException(nameof(LoaderWorker));
    }
}
