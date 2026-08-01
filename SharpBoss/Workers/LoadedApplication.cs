using System;
using System.Threading;
using SharpBoss.Models;

namespace SharpBoss.Workers;

internal sealed class LoadedApplication
{
    private ApplicationLoadContext? _loadContext;
    private LoaderWorker? _worker;

    public LoadedApplication(
        string name,
        string deploymentDirectory,
        ApplicationLoadContext loadContext,
        LoaderWorker worker)
    {
        Name = name;
        DeploymentDirectory = deploymentDirectory;
        _loadContext = loadContext;
        _worker = worker;
    }

    public string Name { get; }

    public string DeploymentDirectory { get; }

    public bool ContainsEndpoint(string path, string method)
    {
        return GetWorker().ContainsEndPoint(path, method);
    }

    public RestResponse Process(string path, string method, RestRequest request)
    {
        return GetWorker().Process(path, method, request);
    }

    public RetiredApplication Unload()
    {
        Interlocked.Exchange(ref _worker, null)?.Dispose();

        var loadContext = Interlocked.Exchange(ref _loadContext, null);
        if (loadContext is null)
        {
            return new RetiredApplication(new WeakReference(null), DeploymentDirectory);
        }

        var reference = new WeakReference(loadContext, trackResurrection: false);
        loadContext.Unload();
        return new RetiredApplication(reference, DeploymentDirectory);
    }

    private LoaderWorker GetWorker()
    {
        return Volatile.Read(ref _worker)
            ?? throw new ObjectDisposedException($"Application '{Name}' has been unloaded.");
    }
}

internal sealed record RetiredApplication(WeakReference LoadContext, string DeploymentDirectory);
