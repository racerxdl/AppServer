using ASAttrib.Models;
using ASTools;
using ASTools.AS;
using ASTools.Logger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace AppServer.Server {
  public class ApplicationManager {

    private static Logger LOG = new Logger(typeof(ApplicationManager));

    private Dictionary<string, AppDomain> appDomains;
    private Dictionary<string, LoaderWorker> loaderWorkers;

    private FileSystemWatcher watcher;

    public ApplicationManager() {
      this.appDomains = new Dictionary<string, AppDomain>();
      this.loaderWorkers = new Dictionary<string, LoaderWorker>();
      this.watcher = new FileSystemWatcher();

      scanAndInitialize();

      watcher.Path = Path.Combine(".", "apps");
      LOG.i("Attaching FileSystemWatcher to " + watcher.Path);
      watcher.NotifyFilter = NotifyFilters.LastAccess
               | NotifyFilters.LastWrite
               | NotifyFilters.FileName
               | NotifyFilters.DirectoryName;
      watcher.Filter = "*.*";
      watcher.Changed += new FileSystemEventHandler(onChanged);
      watcher.Created += new FileSystemEventHandler(onCreated);
      watcher.Deleted += new FileSystemEventHandler(onChanged);
      watcher.Renamed += new RenamedEventHandler(onRenamed);
      watcher.IncludeSubdirectories = true;
      watcher.EnableRaisingEvents = true;
    }

    private AppDomain createDomain(string name, string folder) {
      AppDomainSetup domaininfo = new AppDomainSetup();
      domaininfo.ApplicationBase = folder;
      Evidence adevidence = AppDomain.CurrentDomain.Evidence;
      return AppDomain.CreateDomain(name, adevidence, domaininfo);
    }

    public bool containsEndPoint(string appName, string path, string method) {
      return loaderWorkers.ContainsKey(appName) && loaderWorkers[appName].containsEndPoint(path, method);
    }

    public RestResult callEndPoint(string appName, string path, string method, RestRequest request) {
      return loaderWorkers[appName].callEndpoint(path, method, request);
    }

    private LoaderWorker createLoaderWorker(AppDomain domain) {
      Type lwt = typeof(LoaderWorker);
      return (LoaderWorker)domain.CreateInstanceAndUnwrap(lwt.Assembly.FullName, lwt.FullName);
    }
    private void scanAndInitialize() {
      List<string> apps = Directory.GetDirectories(Path.Combine(".", "apps")).Select(a => Path.GetFileName(a)).ToList();
      foreach (string app in apps) {
        loadApplication(app);
      }
    }
    private void onCreated(object source, FileSystemEventArgs e) {
      LOG.i("onCreated Event: " + e.FullPath);
    }
    private void onDeleted(object source, FileSystemEventArgs e) {
      LOG.i("onDeleted Event: " + e.FullPath);
    }
    private void onChanged(object source, FileSystemEventArgs e) {
      LOG.i("onChanged Event: " + e.FullPath);
    }
    private void onRenamed(object source, RenamedEventArgs e) {
      LOG.i("onRenamed Event: " + e.OldFullPath + " to " + e.FullPath);
    }

    private void loadApplication(string appName) {
      if (appDomains.ContainsKey(appName)) {
        LOG.i("Unloading " + appName);
        loaderWorkers.Remove(appName);
        AppDomain.Unload(appDomains[appName]);
        appDomains.Remove(appName);
      }

      LOG.i("Deploying " + appName);
      AppDomain domain = createDomain(appName, Environment.CurrentDirectory);
      appDomains.Add(appName, domain);

      LoaderWorker lw = createLoaderWorker(domain);

      List<string> assemblies = Directory.GetFiles(Path.Combine(".", "apps", appName)).ToList();
      assemblies.ForEach(x => {
        string targetPath = Path.Combine(".", "deployed", appName);
        string targetFile = Path.Combine(targetPath, Path.GetFileName(x));
        if (!Directory.Exists(targetPath)) {
          Directory.CreateDirectory(targetPath);
        }

        if (File.Exists(targetFile)) {
          File.Delete(targetFile);
        }

        LOG.i("Found " + x + ". Copying to " + targetFile);
        File.Copy(x, targetFile);
        lw.loadAssembly(targetFile);
      });

      loaderWorkers.Add(appName, lw);
    }
  }
}
