using System;
using System.IO;
using System.Linq;
using System.Reflection;

using SharpBoss.Logging;
using SharpBoss.Models;
using SharpBoss.Processors;

namespace SharpBoss.Workers {
  /// <summary>
  /// Load application dynamically
  /// </summary>
  public class LoaderWorker : MarshalByRefObject {
    private RestProcessor _restProcessor;

    /// <summary>
    /// Initialize loader worker for current app domain
    /// </summary>
    public LoaderWorker () {
      Logger.Info (string.Format (
        "LoaderWorker created in AppDomain \"{0}\"", AppDomain.CurrentDomain.FriendlyName
      ));

      _restProcessor = new RestProcessor ();
    }

    /// <summary>
    /// Load Assembly from assembly path
    /// </summary>
    /// <param name="assemblyPath"></param>
    public void LoadAssembly (string assemblyPath) {
      var assembly = GetAssembly (assemblyPath);

      var namespaces = assembly.GetTypes ()
                       .Select (x => x.Namespace)
                       .Distinct ()
                       .ToList ();

      foreach (var nameSpace in namespaces) {
        Logger.Info ("Loading REST calls for " + nameSpace);
        _restProcessor.Init (assembly, nameSpace);
      }
    }

    /// <summary>
    /// Load assembly from path
    /// </summary>
    /// <param name="assemblyPath">Assembly path</param>
    /// <returns>Returns assembly from path</returns>
    public Assembly LoadAssemblyFromPath (string assemblyPath) {
      try {
        return Assembly.LoadFrom (assemblyPath);
      } catch (Exception ex) {
        throw new InvalidOperationException (ex.Message, ex);
      }
    }

    /// <summary>
    /// Retrieve if endpoint is already defined
    /// </summary>
    /// <param name="path">Request path</param>
    /// <param name="method">HTTP method</param>
    /// <returns>True or false</returns>
    public bool ContainsEndPoint (string path, string method) {
      return _restProcessor.ContainsEndPoint (path, method);
    }

    /// <summary>
    /// Process endpoint request and retrieve a Rest Response properly
    /// </summary>
    /// <param name="path">Request path</param>
    /// <param name="method">HTTP method</param>
    /// <param name="request">Rest Request</param>
    /// <returns>Rest Response</returns>
    public RestResponse Process (string path, string method, RestRequest request) {
      try {
        return _restProcessor.Process (path, method, request);
      } catch (Exception ex) {
        var exceptionName = ex.InnerException.GetType ().Name;
        IRestExceptionHandler handler = _restProcessor.GetExceptionHandler (exceptionName);

        if (handler != null) {
          return handler.HandleException (ex.InnerException);
        }

        throw ex;
      }
    }

    /// <summary>
    /// Retrieve assembly from file
    /// </summary>
    /// <param name="assemblyPath">Assembly path</param>
    /// <returns>Assembly</returns>
    private Assembly GetAssembly (string assemblyPath) {
      var targetPath = Path.Combine (AppDomain.CurrentDomain.BaseDirectory, assemblyPath);

      try {
        Assembly assembly;
        Logger.Debug ("Trying to load file " + targetPath);

        var rawAssembly = LoadFile (targetPath);
        var rawSymbolStore = LoadFile (targetPath.Replace (".dll", Utils.DEBUG_SYMBOLS_EXTENSION));

        if (rawSymbolStore != null) {
          assembly = AppDomain.CurrentDomain.Load (rawAssembly, rawSymbolStore);
        } else {
          assembly = AppDomain.CurrentDomain.Load (rawAssembly);
        }

        Logger.Info ("Loaded " + assembly.FullName);
        return assembly;
      } catch (Exception ex) {
        Logger.Error (string.Format("Error loading {0}: {1}", assemblyPath, ex.Message));
        return null;
      }
    }

    /// <summary>
    /// Load file
    /// </summary>
    /// <param name="filename">Filename</param>
    /// <returns>Raw data from file</returns>
    private static byte[] LoadFile (string filename) {
      try {
        var fs = new FileStream (filename, FileMode.Open);
        var buffer = new byte[(int)fs.Length];

        fs.Read (buffer, 0, buffer.Length);
        fs.Close ();

        return buffer;
      } catch (IOException) {
        Logger.Warn ("Cannot find file: " + filename);
        return null;
      }
    }
  }
}
