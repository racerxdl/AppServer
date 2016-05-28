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

    public ApplicationManager() {
      this.appDomains = new Dictionary<string, AppDomain>();
      this.loaderWorkers = new Dictionary<string, LoaderWorker>();
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

    public void loadApplication(string appName) {
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
