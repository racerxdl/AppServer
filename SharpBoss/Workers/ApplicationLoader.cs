using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using SharpBoss.JsonRuntime;
using SharpBoss.Logging;
using SharpBoss.Models;
using SharpBoss.Runtime;

namespace SharpBoss.Workers;

internal sealed class ApplicationLoader : IDisposable
{
    private static readonly StringComparer ApplicationNameComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
    private static readonly string JsonRuntimeAssemblyName =
        typeof(ApplicationJsonSerializer).Assembly.GetName().Name!;
    private static readonly string JsonSerializerAssemblyName =
        typeof(JsonSerializer).Assembly.GetName().Name!;


    private readonly Dictionary<string, LoadedApplication> _applications = new(ApplicationNameComparer);
    private readonly ReaderWriterLockSlim _applicationsLock = new(LockRecursionPolicy.NoRecursion);
    private readonly string _applicationsDirectory;
    private readonly string _deploymentDirectory;
    private readonly FileSystemWatcher _watcher;
    private readonly Timer _reloadTimer;
    private readonly TimeSpan _reloadDebounce;
    private readonly object _reloadGate = new();
    private readonly object _pendingGate = new();
    private readonly HashSet<string> _pendingApplications = new(ApplicationNameComparer);
    private readonly List<RetiredApplication> _retiredApplications = new();
    private volatile bool _disposed;

    public ApplicationLoader(SharpBossOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        _applicationsDirectory = Path.GetFullPath(options.ApplicationsDirectory);
        _deploymentDirectory = Path.GetFullPath(options.DeploymentDirectory);
        _reloadDebounce = options.ReloadDebounce;

        Directory.CreateDirectory(_applicationsDirectory);
        Directory.CreateDirectory(_deploymentDirectory);

        _reloadTimer = new Timer(
            static state => ((ApplicationLoader)state!).ProcessPendingChanges(),
            this,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
        _watcher = new FileSystemWatcher(_applicationsDirectory)
        {
            Filter = "*",
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.CreationTime
                | NotifyFilters.DirectoryName
                | NotifyFilters.FileName
                | NotifyFilters.LastWrite
                | NotifyFilters.Size,
        };

        LoadInitialApplications();
        DefineEvents();
        _watcher.EnableRaisingEvents = true;
        Logger.Info($"Watching applications in '{_applicationsDirectory}'.");
    }

    internal WeakReference? LastRetiredLoadContext { get; private set; }

    public void ForceReload()
    {
        lock (_reloadGate)
        {
            ThrowIfDisposed();

            lock (_pendingGate)
            {
                _pendingApplications.Clear();
            }

            var desiredApplications = GetApplicationDirectories()
                .Select(path => (Name: Path.GetFileName(path), Path: path))
                .Where(application => !string.IsNullOrEmpty(application.Name))
                .ToDictionary(application => application.Name!, application => application.Path, ApplicationNameComparer);
            var errors = new List<Exception>();

            foreach (var loadedApplication in GetLoadedApplicationNames())
            {
                if (!desiredApplications.ContainsKey(loadedApplication))
                {
                    RemoveApplicationCore(loadedApplication);
                }
            }

            foreach (var applicationName in desiredApplications.Keys.OrderBy(name => name, ApplicationNameComparer))
            {
                try
                {
                    ReloadApplicationCore(applicationName);
                }
                catch (Exception exception)
                {
                    Logger.Error($"Failed force-reloading application '{applicationName}'.", exception);
                    errors.Add(new InvalidOperationException(
                        $"Failed force-reloading application '{applicationName}'.",
                        exception));
                }
            }

            CleanupRetiredApplications();

            if (errors.Count > 0)
            {
                throw new AggregateException("One or more applications failed to reload.", errors);
            }
        }
    }

    public bool TryProcess(
        string applicationName,
        string path,
        string method,
        RestRequest request,
        out RestResponse? response)
    {
        _applicationsLock.EnterReadLock();
        try
        {
            if (!_applications.TryGetValue(applicationName, out var application)
                || !application.ContainsEndpoint(path, method))
            {
                response = null;
                return false;
            }

            response = application.Process(path, method, request);
            return true;
        }
        finally
        {
            _applicationsLock.ExitReadLock();
        }
    }

    public void Dispose()
    {
        lock (_reloadGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _reloadTimer.Dispose();

            List<LoadedApplication> applications;
            _applicationsLock.EnterWriteLock();
            try
            {
                applications = _applications.Values.ToList();
                _applications.Clear();
            }
            finally
            {
                _applicationsLock.ExitWriteLock();
            }

            foreach (var application in applications)
            {
                Retire(application);
            }

            CleanupRetiredApplications();
            _applicationsLock.Dispose();
        }
    }

    internal void CleanupRetiredApplications()
    {
        for (var index = _retiredApplications.Count - 1; index >= 0; index--)
        {
            var retired = _retiredApplications[index];
            if (retired.LoadContext.IsAlive || !TryDeleteDirectory(retired.DeploymentDirectory))
            {
                continue;
            }

            _retiredApplications.RemoveAt(index);
            var parentDirectory = Directory.GetParent(retired.DeploymentDirectory)?.FullName;
            if (parentDirectory is not null)
            {
                TryDeleteDirectory(parentDirectory, onlyWhenEmpty: true);
            }
        }
    }

    private void LoadInitialApplications()
    {
        foreach (var applicationDirectory in GetApplicationDirectories())
        {
            var applicationName = Path.GetFileName(applicationDirectory);
            try
            {
                ReloadApplicationCore(applicationName);
            }
            catch (Exception exception)
            {
                Logger.Error($"Failed loading application '{applicationName}'.", exception);
            }
        }
    }

    private void DefineEvents()
    {
        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Renamed += OnRenamed;
        _watcher.Error += OnWatcherError;
    }

    private void OnChanged(object sender, FileSystemEventArgs eventArgs)
    {
        ScheduleReload(eventArgs.FullPath);
    }

    private void OnRenamed(object sender, RenamedEventArgs eventArgs)
    {
        ScheduleReload(eventArgs.OldFullPath);
        ScheduleReload(eventArgs.FullPath);
    }

    private void OnWatcherError(object sender, ErrorEventArgs eventArgs)
    {
        Logger.Error("The application file watcher reported an error.", eventArgs.GetException());
        ScheduleAllApplications();
    }

    private void ScheduleReload(string changedPath)
    {
        if (_disposed)
        {
            return;
        }

        var applicationName = GetApplicationName(changedPath);
        if (applicationName is null)
        {
            return;
        }

        lock (_pendingGate)
        {
            _pendingApplications.Add(applicationName);
        }

        try
        {
            _reloadTimer.Change(_reloadDebounce, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException) when (_disposed)
        {
        }
    }

    private void ScheduleAllApplications()
    {
        if (_disposed)
        {
            return;
        }

        lock (_pendingGate)
        {
            foreach (var directory in GetApplicationDirectories())
            {
                _pendingApplications.Add(Path.GetFileName(directory));
            }

            foreach (var applicationName in GetLoadedApplicationNames())
            {
                _pendingApplications.Add(applicationName);
            }
        }

        try
        {
            _reloadTimer.Change(_reloadDebounce, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException) when (_disposed)
        {
        }
    }

    private void ProcessPendingChanges()
    {
        lock (_reloadGate)
        {
            if (_disposed)
            {
                return;
            }

            string[] applicationNames;
            lock (_pendingGate)
            {
                applicationNames = _pendingApplications.ToArray();
                _pendingApplications.Clear();
            }

            foreach (var applicationName in applicationNames.OrderBy(name => name, ApplicationNameComparer))
            {
                try
                {
                    var applicationDirectory = Path.Combine(_applicationsDirectory, applicationName);
                    if (Directory.Exists(applicationDirectory))
                    {
                        ReloadApplicationCore(applicationName);
                    }
                    else
                    {
                        RemoveApplicationCore(applicationName);
                    }
                }
                catch (Exception exception)
                {
                    Logger.Error(
                        $"Failed reloading application '{applicationName}'; the previous generation remains active.",
                        exception);
                }
            }

            CleanupRetiredApplications();
        }
    }

    private void ReloadApplicationCore(string applicationName)
    {
        var replacement = BuildApplication(applicationName);
        LoadedApplication? previous;

        _applicationsLock.EnterWriteLock();
        try
        {
            _applications.Remove(applicationName, out previous);
            _applications.Add(applicationName, replacement);
        }
        finally
        {
            _applicationsLock.ExitWriteLock();
        }

        if (previous is not null)
        {
            Retire(previous);
        }

        Logger.Info($"Application '{applicationName}' is active from '{replacement.DeploymentDirectory}'.");
    }

    private void RemoveApplicationCore(string applicationName)
    {
        LoadedApplication? removed;
        _applicationsLock.EnterWriteLock();
        try
        {
            _applications.Remove(applicationName, out removed);
        }
        finally
        {
            _applicationsLock.ExitWriteLock();
        }

        if (removed is null)
        {
            return;
        }

        Retire(removed);
        Logger.Info($"Application '{applicationName}' was removed.");
    }

    private LoadedApplication BuildApplication(string applicationName)
    {
        var sourceDirectory = Path.Combine(_applicationsDirectory, applicationName);
        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException($"Application directory '{sourceDirectory}' does not exist.");
        }

        var generationDirectory = Path.Combine(
            _deploymentDirectory,
            applicationName,
            $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}");
        CopyDirectory(sourceDirectory, generationDirectory);
        var jsonRuntimePath = CopyRuntimeDependency(
            typeof(ApplicationJsonSerializer).Assembly,
            generationDirectory);
        CopyRuntimeDependency(typeof(JsonSerializer).Assembly, generationDirectory);

        var candidates = Directory
            .EnumerateFiles(generationDirectory, "*.dll", SearchOption.TopDirectoryOnly)
            .Select(TryCreateAssemblyCandidate)
            .OfType<AssemblyCandidate>()
            .OrderBy(candidate => candidate.Name.Name, StringComparer.Ordinal)
            .ToArray();
        var applicationCandidates = candidates
            .Where(candidate => !IsRuntimeInfrastructure(candidate.Name))
            .ToArray();

        if (applicationCandidates.Length == 0)
        {
            TryDeleteDirectory(generationDirectory);
            throw new InvalidOperationException($"Application '{applicationName}' contains no managed assemblies.");
        }

        var sharedAssemblies = new[]
        {
            typeof(global::SharpBoss.SharpBoss).Assembly,
            typeof(Logger).Assembly,
            typeof(IApplicationJsonSerializer).Assembly,
        };
        ApplicationLoadContext? loadContext = null;
        IApplicationJsonSerializer? jsonSerializer = null;
        LoaderWorker? worker = null;

        try
        {
            loadContext = new ApplicationLoadContext(
                applicationName,
                generationDirectory,
                candidates.Select(candidate => candidate.Path),
                sharedAssemblies);
            var jsonRuntimeAssembly = loadContext.LoadFromAssemblyPath(jsonRuntimePath);
            jsonSerializer = CreateJsonSerializer(jsonRuntimeAssembly);
            worker = new LoaderWorker(jsonSerializer);
            jsonSerializer = null;

            foreach (var candidate in applicationCandidates)
            {
                if (loadContext.IsShared(candidate.Name))
                {
                    continue;
                }

                var assembly = loadContext.Assemblies.FirstOrDefault(
                    loaded => AssemblyName.ReferenceMatchesDefinition(loaded.GetName(), candidate.Name))
                    ?? loadContext.LoadFromAssemblyPath(candidate.Path);
                worker.LoadAssembly(assembly);
            }

            if (worker.EndpointCount == 0)
            {
                throw new InvalidOperationException($"Application '{applicationName}' defines no REST endpoints.");
            }

            return new LoadedApplication(applicationName, generationDirectory, loadContext, worker);
        }
        catch
        {
            jsonSerializer?.Dispose();
            worker?.Dispose();
            if (loadContext is not null)
            {
                Retire(loadContext, generationDirectory);
            }
            else
            {
                TryDeleteDirectory(generationDirectory);
            }

            throw;
        }
    }

    private static IApplicationJsonSerializer CreateJsonSerializer(Assembly jsonRuntimeAssembly)
    {
        var serializerType = jsonRuntimeAssembly.GetType(
            typeof(ApplicationJsonSerializer).FullName!,
            throwOnError: true,
            ignoreCase: false)!;
        var serializer = Activator.CreateInstance(serializerType, nonPublic: true);

        return serializer as IApplicationJsonSerializer
            ?? throw new InvalidOperationException(
                $"'{serializerType.FullName}' did not bind to the shared JSON serializer contract.");
    }

    private static string CopyRuntimeDependency(Assembly assembly, string generationDirectory)
    {
        if (string.IsNullOrEmpty(assembly.Location))
        {
            throw new InvalidOperationException($"Runtime dependency '{assembly.FullName}' has no file location.");
        }

        var destinationPath = Path.Combine(generationDirectory, Path.GetFileName(assembly.Location));
        File.Copy(assembly.Location, destinationPath, overwrite: true);
        return destinationPath;
    }

    private static bool IsRuntimeInfrastructure(AssemblyName assemblyName)
    {
        return string.Equals(assemblyName.Name, JsonRuntimeAssemblyName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(assemblyName.Name, JsonSerializerAssemblyName, StringComparison.OrdinalIgnoreCase);
    }

    private void Retire(LoadedApplication application)
    {
        var retired = application.Unload();
        LastRetiredLoadContext = retired.LoadContext;
        _retiredApplications.Add(retired);
    }

    private void Retire(ApplicationLoadContext loadContext, string deploymentDirectory)
    {
        var reference = new WeakReference(loadContext, trackResurrection: false);
        loadContext.Unload();
        LastRetiredLoadContext = reference;
        _retiredApplications.Add(new RetiredApplication(reference, deploymentDirectory));
    }

    private IEnumerable<string> GetApplicationDirectories()
    {
        return Directory
            .EnumerateDirectories(_applicationsDirectory)
            .OrderBy(Path.GetFileName, ApplicationNameComparer);
    }

    private string[] GetLoadedApplicationNames()
    {
        _applicationsLock.EnterReadLock();
        try
        {
            return _applications.Keys.ToArray();
        }
        finally
        {
            _applicationsLock.ExitReadLock();
        }
    }

    private string? GetApplicationName(string changedPath)
    {
        var relativePath = Path.GetRelativePath(_applicationsDirectory, changedPath);
        if (relativePath is "." or ".." || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return null;
        }

        var separatorIndex = relativePath.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        return separatorIndex < 0 ? relativePath : relativePath[..separatorIndex];
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath, overwrite: true);
        }
    }

    private static AssemblyCandidate? TryCreateAssemblyCandidate(string path)
    {
        try
        {
            return new AssemblyCandidate(path, AssemblyName.GetAssemblyName(path));
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (FileLoadException)
        {
            return null;
        }
    }

    private static bool TryDeleteDirectory(string path, bool onlyWhenEmpty = false)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return true;
            }

            if (onlyWhenEmpty && Directory.EnumerateFileSystemEntries(path).Any())
            {
                return false;
            }

            Directory.Delete(path, recursive: !onlyWhenEmpty);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed record AssemblyCandidate(string Path, AssemblyName Name);
}
