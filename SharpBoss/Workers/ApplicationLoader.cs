using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Timers;

using SharpBoss.Logging;
using SharpBoss.Models;

namespace SharpBoss.Workers
{
    /// <summary>
    /// 
    /// </summary>
    internal class ApplicationLoader
    {
        Timer _timer;

        Dictionary<string, AppDomain> _appDomains;
        Dictionary<string, LoaderWorker> _loaderWorkers;
        Dictionary<string, AppAction> _appActions;

        FileSystemWatcher _watcher;

        string _appsDir;
        string _deployedDir;

        /// <summary>
        /// Start the Rest watcher
        /// </summary>
        public ApplicationLoader()
        {
            this._appDomains = new Dictionary<string, AppDomain>();
            this._loaderWorkers = new Dictionary<string, LoaderWorker>();
            this._watcher = new FileSystemWatcher();
            this._appActions = new Dictionary<string, AppAction>();
            this._appsDir = Path.Combine(".", "apps");
            this._deployedDir = Path.Combine(".", "deployed");

            this.Init();

            this._timer = new Timer();
            this._timer.Interval = 5 * 1000;

            this._watcher.Path = Path.Combine(".", "apps");
            Logger.Info("Attaching FileSystemWatcher to " + this._watcher.Path);

            this._watcher.NotifyFilter = NotifyFilters.LastAccess
                     | NotifyFilters.LastWrite
                     | NotifyFilters.FileName
                     | NotifyFilters.DirectoryName;

            this._watcher.Filter = "*.*";
            this._watcher.IncludeSubdirectories = true;
            this._watcher.EnableRaisingEvents = true;
            DefineEvents();
        }
        public void ForceReload()
        {
            lock (this._appDomains)
            {
                Logger.Info("Force reloading apps...");
                var keys = this._appDomains.Keys.ToList();
                foreach (var app in keys)
                {
                    LoadApplication(app); // Load also unloads first
                }
            }
        }
        /// <summary>
        /// Define all event handlers
        /// </summary>
        private void DefineEvents()
        {
            this._timer.Elapsed += Timer_Elapsed;
            this._watcher.Changed += new FileSystemEventHandler(onChanged);
            this._watcher.Created += new FileSystemEventHandler(onCreated);
            this._watcher.Deleted += new FileSystemEventHandler(onChanged);
            this._watcher.Renamed += new RenamedEventHandler(onRenamed);
        }

        /// <summary>
        /// Event triggered after start a timer to update assembly
        /// </summary>
        /// <param name="sender">Event source</param>
        /// <param name="e">Event arguments</param>
        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Logger.Info("AppTimer Elapsed. Checking for application changes.");

            var appActions = new List<AppAction>();

            lock (this._appActions)
            {
                var applicationNames = this._appActions.Keys.Select(x => x).ToArray();

                foreach (var applicationName in applicationNames)
                {
                    appActions.Add(this._appActions[applicationName]);
                    this._appActions.Remove(applicationName);
                }
            }

            Logger.Info(string.Format("We have {0} application actions to do. Starting...", appActions.Count));

            lock (this._appDomains)
            {
                lock (this._loaderWorkers)
                {
                    foreach (var appAction in appActions)
                    {
                        if (appAction.IsRemoved)
                        {
                            UnloadApplication(appAction.ApplicationName);
                            Logger.Info(string.Format("Application {0} removed from pool", appAction.ApplicationName));
                        }
                        else
                        {
                            Logger.Info(string.Format("Application {0} is avaliable", appAction.ApplicationName));
                            LoadApplication(appAction.ApplicationName);
                        }
                    }
                }
            }

            this._timer.AutoReset = false;
            this._timer.Enabled = false;
        }

        /// <summary>
        /// Create new application domain from folder with name
        /// </summary>
        /// <param name="applicationName">Application Name</param>
        /// <param name="applicationPath">Application path</param>
        /// <returns></returns>
        private AppDomain CreateApplicationDomain(string applicationName, string applicationPath)
        {
            Logger.Debug(
              string.Format(
                "Creating application domain for {0} from {1}", applicationName, applicationPath
              )
            );

            //      Assembly.LoadFrom(applicationPath);
            //      var applicationContext = AssemblyLoadContext.Default; // new AssemblyLoadContext (applicationName, true);
            //var assembly = applicationContext.LoadFromAssemblyPath (applicationPath);

            var appDomain = AppDomain.CurrentDomain;
            appDomain.Load(AssemblyName.GetAssemblyName(applicationPath));

            return appDomain;
        }

        /// <summary>
        /// Validate if endpoint already exists inside Application
        /// </summary>
        /// <param name="appName">Application name</param>
        /// <param name="path">Request path</param>
        /// <param name="method">HTTP Method</param>
        /// <returns>Returns true or false</returns>
        public bool ContainsEndPoint(string appName, string path, string method)
        {
            return this._loaderWorkers.ContainsKey(appName) &&
              this._loaderWorkers[appName].ContainsEndPoint(path, method);
        }

        /// <summary>
        /// Process endpoint request from loader Application
        /// </summary>
        /// <param name="appName">Application name</param>
        /// <param name="path">Request path</param>
        /// <param name="method">HTTP Method</param>
        /// <param name="request">Rest Request</param>
        /// <returns>Returns a new Rest Response</returns>
        public RestResponse Process(string appName, string path, string method, RestRequest request)
        {
            var loaderWorker = this._loaderWorkers[appName];

            if (loaderWorker != null)
            {
                return loaderWorker.Process(path, method, request);
            }

            return new RestResponse("LoaderWorker not initialized", "text/plain", System.Net.HttpStatusCode.InternalServerError);
        }

        /// <summary>
        /// Create new LoaderWorker from Application Domain
        /// </summary>
        /// <param name="appDomain">Application Domain</param>
        /// <returns>Returns a new instance of LoaderWorker</returns>
        private LoaderWorker CreateLoaderWorker(AppDomain appDomain)
        {
            var type = typeof(LoaderWorker);
            return (LoaderWorker)appDomain.CreateInstanceAndUnwrap(type.Assembly.FullName, type.FullName);
        }

        /// <summary>
        /// Initialize the process and load all applications from application directory
        /// </summary>
        private void Init()
        {
            if (!Directory.Exists(this._appsDir))
            {
                Directory.CreateDirectory(this._appsDir);
            }

            var applications = Directory.GetDirectories(this._appsDir);
            var applicationNames = applications
                                   .Select(a => Path.GetFileName(a))
                                   .ToList();

            foreach (var applicationName in applicationNames)
            {
                LoadApplication(applicationName);
            }
        }

        /// <summary>
        /// Event handler to trigger when a file is created
        /// </summary>
        /// <param name="source">Event source</param>
        /// <param name="e">Event arguments</param>
        private void onCreated(object source, FileSystemEventArgs e)
        {
            if (!e.FullPath.Equals(this._appsDir))
            {
                ScheduleRefreshApplication(GetApplicationNameFromFolder(e.FullPath), false);
            }
        }

        /// <summary>
        /// Event handler to trigger when a file is deleted
        /// </summary>
        /// <param name="source">Event source</param>
        /// <param name="e">Event arguments</param>
        private void onDeleted(object source, FileSystemEventArgs e)
        {
            if (!e.FullPath.Equals(this._appsDir))
            {
                ScheduleRefreshApplication(GetApplicationNameFromFolder(e.FullPath), true);
            }
        }

        /// <summary>
        /// Event handler to trigger when a file is changed
        /// </summary>
        /// <param name="source">Event source</param>
        /// <param name="e">Event arguments</param>
        private void onChanged(object source, FileSystemEventArgs e)
        {
            if (!e.FullPath.Equals(this._appsDir))
            {
                ScheduleRefreshApplication(GetApplicationNameFromFolder(e.FullPath), !(File.Exists(e.FullPath) || Directory.Exists(e.FullPath)));
            }
        }

        /// <summary>
        /// Event handler to trigger when a file is renamed
        /// </summary>
        /// <param name="source">Event source</param>
        /// <param name="e">Event arguments</param>
        private void onRenamed(object source, RenamedEventArgs e)
        {
            ScheduleRefreshApplication(GetApplicationNameFromFolder(e.OldFullPath), true);
            ScheduleRefreshApplication(GetApplicationNameFromFolder(e.FullPath), false);
        }

        /// <summary>
        /// Schedule application refresh when 
        /// </summary>
        /// <param name="applicationName"></param>
        /// <param name="isRemoved"></param>
        private void ScheduleRefreshApplication(string applicationName, bool isRemoved)
        {
            Logger.Info("Detected change for " + applicationName);

            lock (this._appActions)
            {
                if (this._appActions.ContainsKey(applicationName))
                {
                    this._appActions[applicationName].ApplicationName = applicationName;
                    this._appActions[applicationName].IsRemoved = isRemoved;
                }
                else
                {
                    this._appActions.Add(applicationName, new AppAction(applicationName, isRemoved));
                }
            }

            this._timer.Enabled = true;
            this._timer.Start();
        }

        /// <summary>
        /// Get application name from folder
        /// </summary>
        /// <param name="folder">Application folder</param>
        /// <returns>Application name</returns>
        private string GetApplicationNameFromFolder(string folder)
        {
            if (File.Exists(folder))
            {
                folder = Path.GetDirectoryName(folder);
            }

            return folder
                  .Substring(folder.LastIndexOf("apps" + Utils.DIRECTORY_SEPARATOR))
                  .Split(Utils.DIRECTORY_SEPARATOR)[1];
        }

        /// <summary>
        /// Unload application by name
        /// </summary>
        /// <param name="applicationName">Application name</param>
        private void UnloadApplication(string applicationName)
        {
            if (this._appDomains.ContainsKey(applicationName))
            {
                Logger.Info("Unloading " + applicationName);
                try
                {
                    this._loaderWorkers.Remove(applicationName);
                    AppDomain.Unload(this._appDomains[applicationName]);
                    this._appDomains.Remove(applicationName);

                    var directory = new DirectoryInfo(Path.Combine(".", "deployed", applicationName));
                    directory.Delete(true);
                }
                catch (Exception ex)
                {
                    Logger.Error(string.Format("Failed unloading application {0}:\n{1}", applicationName, ex.ToString()));
                }
            }
        }
        private AppDomain createDomain(string name, string folder)
        {
            AppDomainSetup domaininfo = new AppDomainSetup();
            domaininfo.ApplicationBase = folder;
            Evidence adevidence = AppDomain.CurrentDomain.Evidence;
            return AppDomain.CreateDomain(name, adevidence, domaininfo);
        }
        /// <summary>
        /// Deploy application from name
        /// </summary>
        /// <param name="applicationName"></param>
        private void LoadApplication(string applicationName)
        {
            UnloadApplication(applicationName);

            Logger.Info("Deploying application: " + applicationName);

            var appPath = Path.Combine(Environment.CurrentDirectory, this._appsDir, applicationName);
            var assemblies = Directory.GetFiles(appPath)
                             .Where(x => x.EndsWith(".dll"))
                             .ToList();
            var appDomain = createDomain(applicationName, Environment.CurrentDirectory);
            this._appDomains.Add(applicationName, appDomain);
            LoaderWorker loaderWorker = new LoaderWorker();

            assemblies.ForEach(appAssembly =>
            {
                string targetPath = Path.Combine(this._deployedDir, applicationName);
                string targetFile = Path.Combine(targetPath, Path.GetFileName(appAssembly));
                string appDebugAssembly = appAssembly.Replace(".dll", Utils.DEBUG_SYMBOLS_EXTENSION);
                string targetDebugFile = Path.Combine(targetPath, Path.GetFileName(appDebugAssembly));
                try
                {
                    if (!Directory.Exists(targetPath))
                    {
                        Directory.CreateDirectory(targetPath);
                    }

                    if (File.Exists(targetFile))
                    {
                        File.Delete(targetFile);
                    }

                    if (File.Exists(targetDebugFile))
                    {
                        File.Delete(targetDebugFile);
                    }

                    Logger.Info(string.Format("Found {0}. Copying to {1}", appAssembly, targetFile));
                    File.Copy(appAssembly, targetFile);

                    if (File.Exists(appDebugAssembly))
                    {
                        Logger.Info(string.Format("Found debug file for {0} at {1}", appAssembly, appDebugAssembly));
                        File.Copy(appDebugAssembly, targetDebugFile);
                    }

                    loaderWorker.LoadAssembly(targetFile);
                }
                catch (Exception ex)
                {
                    Logger.Error(string.Format("Failed loading application assembly {0}:\n{1}", appAssembly, ex.ToString()));
                }
            });

            this._loaderWorkers.Add(applicationName, loaderWorker);
        }
    }
}