using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace SharpBoss.Workers;

internal sealed class ApplicationLoadContext : AssemblyLoadContext
{
    private readonly string _applicationDirectory;
    private readonly AssemblyDependencyResolver[] _resolvers;
    private readonly IReadOnlyDictionary<string, Assembly> _sharedAssemblies;

    public ApplicationLoadContext(
        string applicationName,
        string applicationDirectory,
        IEnumerable<string> componentAssemblyPaths,
        IEnumerable<Assembly> sharedAssemblies)
        : base($"SharpBoss:{applicationName}:{Guid.NewGuid():N}", isCollectible: true)
    {
        _applicationDirectory = Path.GetFullPath(applicationDirectory);
        _resolvers = componentAssemblyPaths
            .Select(path => new AssemblyDependencyResolver(Path.GetFullPath(path)))
            .ToArray();
        _sharedAssemblies = sharedAssemblies
            .Where(assembly => assembly.GetName().Name is not null)
            .ToDictionary(
                assembly => assembly.GetName().Name!,
                assembly => assembly,
                StringComparer.OrdinalIgnoreCase);
    }

    public bool IsShared(AssemblyName assemblyName)
    {
        return assemblyName.Name is not null && _sharedAssemblies.ContainsKey(assemblyName.Name);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is not null && _sharedAssemblies.TryGetValue(assemblyName.Name, out var sharedAssembly))
        {
            return sharedAssembly;
        }

        foreach (var resolver in _resolvers)
        {
            var resolvedPath = resolver.ResolveAssemblyToPath(assemblyName);
            if (resolvedPath is not null)
            {
                return LoadFromAssemblyPath(resolvedPath);
            }
        }

        if (assemblyName.Name is null)
        {
            return null;
        }

        var localPath = Path.Combine(_applicationDirectory, assemblyName.Name + ".dll");
        return File.Exists(localPath) ? LoadFromAssemblyPath(localPath) : null;
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        foreach (var resolver in _resolvers)
        {
            var resolvedPath = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (resolvedPath is not null)
            {
                return LoadUnmanagedDllFromPath(resolvedPath);
            }
        }

        return 0;
    }
}
