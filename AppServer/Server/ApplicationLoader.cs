﻿using ASAttrib.Models;
using ASTools.Logger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Timers;

namespace AppServer.Server {
  public class ApplicationManager {

    private static Logger LOG = new Logger(typeof(ApplicationManager));

    private Timer appTimer;
    private Dictionary<string, AppDomain> appDomains;
    private Dictionary<string, LoaderWorker> loaderWorkers;

    private Dictionary<string, AppAction> appActions;

    private FileSystemWatcher watcher;

    public ApplicationManager() {
      this.appDomains = new Dictionary<string, AppDomain>();
      this.loaderWorkers = new Dictionary<string, LoaderWorker>();
      this.watcher = new FileSystemWatcher();
      this.appActions = new Dictionary<string, AppAction>();

      scanAndInitialize();

      appTimer = new Timer();
      appTimer.Interval = 5 * 1000;
      appTimer.Elapsed += AppTimer_Elapsed;

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

    private void AppTimer_Elapsed(object sender, ElapsedEventArgs e) {
      LOG.i("AppTimer Elapsed. Checking for application changes.");
      List<AppAction> todo = new List<AppAction>();
      lock (appActions) {
        string[] keys = appActions.Keys.Select(x => x).ToArray();
        foreach (string key in keys) {
          todo.Add(appActions[key]);
          appActions.Remove(key);
        }
      }
      LOG.i(todo.Count + " appActions todo.");
      // Do actions
      lock (appDomains) {
        lock (loaderWorkers) {
          foreach (AppAction action in todo) {
            if (action.ApplicationRemoved) {
              unloadApplication(action.ApplicationName);
              LOG.i("Application " + action.ApplicationName + " removed from pool.");
            } else {
              LOG.i("Application " + action.ApplicationName + " is available.");
              loadApplication(action.ApplicationName);
            }
          }
        }
      }
      appTimer.AutoReset = false;
      appTimer.Enabled = false;
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
      if (!e.FullPath.Equals(Path.Combine(".", "apps"))) {
        scheduleRefreshApplication(appNameFromFolder(e.FullPath), false);
      }
    }
    private void onDeleted(object source, FileSystemEventArgs e) {
      if (!e.FullPath.Equals(Path.Combine(".", "apps"))) {
        scheduleRefreshApplication(appNameFromFolder(e.FullPath), true);
      }
    }
    private void onChanged(object source, FileSystemEventArgs e) {
      if (!e.FullPath.Equals(Path.Combine(".", "apps"))) {
        scheduleRefreshApplication(appNameFromFolder(e.FullPath), !(File.Exists(e.FullPath) || Directory.Exists(e.FullPath)));
      }
    }
    private void onRenamed(object source, RenamedEventArgs e) {
      scheduleRefreshApplication(appNameFromFolder(e.OldFullPath), true);
      scheduleRefreshApplication(appNameFromFolder(e.FullPath), false);
    }

    private void scheduleRefreshApplication(string application, bool removed) {
      LOG.i ("Detected change for " + application);
      lock (appActions) {
        if (appActions.ContainsKey(application)) {
          appActions[application].ApplicationName = application;
          appActions[application].ApplicationRemoved = removed;
        } else {
          appActions.Add(application, new AppAction(application, removed));
        }
      }
      appTimer.Enabled = true;
      appTimer.Start();
    }

    private string appNameFromFolder(string folder) {
      if (File.Exists(folder)) { // A folder changed
        folder = Path.GetDirectoryName(folder);
      }
      return folder.Substring(folder.LastIndexOf("apps" + ASTools.Tools.DIRECTORY_SEPARATOR)).Split(ASTools.Tools.DIRECTORY_SEPARATOR)[1];
    }

    private void unloadApplication(string appName) {
      if (appDomains.ContainsKey(appName)) {
        LOG.i("Unloading " + appName);
        try {
          loaderWorkers.Remove(appName);
          AppDomain.Unload(appDomains[appName]);
          appDomains.Remove(appName);
          var dir = new DirectoryInfo(Path.Combine(".", "deployed", appName));
          dir.Delete(true);
        } catch (Exception e) {
          LOG.e("Failed unloading " + appName + ": \n" + e.ToString());
        }
      }
    }

    private void loadApplication(string appName) {
      unloadApplication(appName);

      LOG.i("Deploying " + appName);
      AppDomain domain = createDomain(appName, Environment.CurrentDirectory);
      appDomains.Add(appName, domain);

      LoaderWorker lw = createLoaderWorker(domain);

      List<string> assemblies = Directory.GetFiles(Path.Combine(".", "apps", appName)).Where(x => x.Contains(".dll")).ToList();
      assemblies.ForEach(appAssembly => {
        string targetPath = Path.Combine(".", "deployed", appName);
        string targetFile = Path.Combine(targetPath, Path.GetFileName(appAssembly));
        string appDebugAssembly = appAssembly.Replace(".dll", ".pdb");
        string targetDebugFile = Path.Combine(targetPath, Path.GetFileName(appDebugAssembly));
        try {
          if (!Directory.Exists(targetPath)) {
            Directory.CreateDirectory(targetPath);
          }

          if (File.Exists(targetFile)) {
            File.Delete(targetFile);
          }

          if (File.Exists(targetDebugFile)) {
            File.Delete(targetDebugFile);
          }

          LOG.i("Found " + appAssembly + ". Copying to " + targetFile);
          File.Copy(appAssembly, targetFile);

          if (File.Exists(appDebugAssembly)) {
            LOG.i("Found debug file for " + appAssembly + " at " + appDebugAssembly);
            File.Copy(appDebugAssembly, targetDebugFile);
          }

          lw.loadAssembly(targetFile);
        } catch (Exception e) {
          LOG.e("Failed loading application assembly " + appAssembly + ": \n" + e.ToString());
        }
      });

      loaderWorkers.Add(appName, lw);
    }
  }
}
