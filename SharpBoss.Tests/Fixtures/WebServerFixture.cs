using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using SharpBoss.Logging;
using SharpBoss.Tests.Fixtures.Templates;

namespace SharpBoss.Tests.Fixtures {
  public static class WebServerFixture {
    public static (string, Assembly) CreateNewServer () {
      var className = Faker.Name.First ();
      var nameSpace = string.Format ("{0}.{1}", Faker.Name.First (), Faker.Name.Last ());
      var projectName = nameSpace.Replace (".", "_").ToLower ();
      var fileName = nameSpace + ".dll";

      var appsPath = Path.Combine (".", "apps");
      var apiPath = Path.Combine (appsPath, projectName);
      var projectFile = Path.Combine (apiPath, fileName);

      if (!Directory.Exists (appsPath)) {
        Directory.CreateDirectory (appsPath);
      }

      if (!Directory.Exists (apiPath)) {
        Directory.CreateDirectory (apiPath);
      }

      Logger.Info (string.Format ("Creating WebServer with namespace {0} and main class {1}", nameSpace, className));

      var template = new WebServerTemplate () {
        Class = className,
        Namespace = nameSpace
      };

      var content = template.TransformText ();

      var references = new List<MetadataReference> ();
      var referencedAssemblies = Assembly.GetEntryAssembly ().GetReferencedAssemblies ();

      foreach (var referencedAssembly in referencedAssemblies) {
        var loadedAssembly = Assembly.Load (referencedAssembly);
        references.Add (MetadataReference.CreateFromFile (loadedAssembly.Location));
      }

      referencedAssemblies = Assembly.GetExecutingAssembly ().GetReferencedAssemblies ();

      foreach (var referencedAssembly in referencedAssemblies) {
        var loadedAssembly = Assembly.Load (referencedAssembly);
        references.Add (MetadataReference.CreateFromFile (loadedAssembly.Location));
      }

      references.Add (MetadataReference.CreateFromFile (typeof (object).GetTypeInfo ().Assembly.Location));

      var compilation =
        CSharpCompilation
          .Create (nameSpace)
          .WithOptions (
            new CSharpCompilationOptions (Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary)
          )
          .AddReferences (references.ToArray ())
          .AddSyntaxTrees (CSharpSyntaxTree.ParseText (content));

      Logger.Info (string.Format ("Saving WebServer to file {0}", fileName));
      var emitResult = compilation.Emit (projectFile);

      if (!emitResult.Success) {
        var diagnostics = emitResult.Diagnostics
                          .Where (diagnostic =>
                            diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error
                          )
                          .ToList ();

        foreach (var diagnostic in diagnostics) {
          Console.WriteLine (diagnostic.GetMessage ());
        }

        throw new Exception ("Couldn't compile");
      }

      return (projectName, Assembly.LoadFrom (projectFile));
    }

    public static dynamic CreateConfigFile (string listenUrl) {
      var fileName = "app.xml";
      var path = Path.Combine (AppDomain.CurrentDomain.BaseDirectory, fileName);

      Logger.Info (string.Format ("Removing {0} from path {1}", fileName, path));
      File.Delete (path);

      var template = new ConfigFileTemplate () {
        ListenUrl = listenUrl
      };

      var content = template.TransformText ();

      Logger.Info (string.Format ("Saving config to file {0}", path));
      File.WriteAllText (path, content, Encoding.UTF8);

      var fileMap = new ExeConfigurationFileMap () {
        ExeConfigFilename = path
      };

      var config = ConfigurationManager.OpenMappedExeConfiguration (fileMap, ConfigurationUserLevel.None);

      return config.AppSettings.Settings;
    }
  }
}
