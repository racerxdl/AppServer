﻿using ASAttrib.Models;
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
      LogManager.initialize(".\\", LogLevel.DEBUG);
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
      return restProcessor.callEndPoint(path, method, request);
    }

    private Assembly getAssembly(string assemblyPath) { 
      string targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assemblyPath);
      try {
        LOG.d("Trying to load file " + targetPath);
        Assembly a = Assembly.LoadFile(targetPath);
        LOG.i("Loaded " + a.FullName);
        return a;
      } catch (Exception e) {
        LOG.e("Error loading " + assemblyPath + ": " + e.Message);
        return null;
      }
    }
  }
}