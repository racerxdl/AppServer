using ASAttrib;
using ASAttrib.Models;
using ASAttrib.Processors;
using ASTools.Logger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AppServer.Server {
  public class LoaderWorker : MarshalByRefObject {

    private static Logger LOG = new Logger(typeof(LoaderWorker));
    private RestProcessor restProcessor;

    public LoaderWorker() {
      LogManager.initialize(".", LogLevel.DEBUG);
      LOG.i(String.Format("LoaderWorker created in AppDomain \"{0}\"", AppDomain.CurrentDomain.FriendlyName));
      restProcessor = new RestProcessor();
    }

    public void loadAssembly(string assemblyPath) {
      Assembly a = getAssembly(assemblyPath);
      string[] namespaces = a.GetTypes().Select(x => x.Namespace).Distinct().ToArray();
      foreach (string n in namespaces) {
        LOG.i("Loading REST calls for " + n);
        restProcessor.init(a, n);
      }
    }

    public bool containsEndPoint(string path, string method) {
      return restProcessor.containsEndPoint(path, method);
    }

    public RestResult callEndpoint(string path, string method, RestRequest request) {
      try {
        return restProcessor.callEndPoint(path, method, request);
      } catch (Exception e) {
        string exceptionName = e.InnerException.GetType().Name;
        IRestExceptionHandler handler = restProcessor.getExceptionHandler(exceptionName);
        if (handler != null) {
          return handler.handleException(e.InnerException);
        }
        throw e;
      }
    }

    private Assembly getAssembly(string assemblyPath) {
      string targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assemblyPath);
      try {
        Assembly assembly;
        LOG.d("Trying to load file " + targetPath);

        byte[] rawAssembly = loadFile(targetPath);
        byte[] rawSymbolStore = loadFile(targetPath.Replace(".dll", ASTools.Tools.DEBUG_SYMBOLS_EXTENSION));

        if (rawSymbolStore != null) {
          assembly = AppDomain.CurrentDomain.Load(rawAssembly, rawSymbolStore);
        } else {
          assembly = AppDomain.CurrentDomain.Load(rawAssembly);
        }

        LOG.i("Loaded " + assembly.FullName);
        return assembly;
      } catch (Exception e) {
        LOG.e("Error loading " + assemblyPath + ": " + e.Message);
        return null;
      }
    }

    private static byte[] loadFile(string filename) {
      try {
        FileStream fs = new FileStream(filename, FileMode.Open);
        byte[] buffer = new byte[(int)fs.Length];
        fs.Read(buffer, 0, buffer.Length);
        fs.Close();

        return buffer;
      } catch (IOException) {
        LOG.w("Cannot find file: " + filename);
        return null;
      }
    }
  }
}
