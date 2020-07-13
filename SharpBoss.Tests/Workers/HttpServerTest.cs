using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;

using NUnit.Framework;

using RestSharp;

using SharpBoss.Logging;
using SharpBoss.Tests.Fixtures;

namespace SharpBoss.Tests.Workers {
  public class HttpServerTest {
    private string _app;
    private Assembly _assembly;
    private SharpBoss _sharpBoss;
    private RestClient _client;

    [SetUp]
    public void Setup () {
      (this._app, this._assembly) = WebServerFixture.CreateNewServer ();
      var listenUrl = "http://localhost:4550";

      this._sharpBoss = new SharpBoss (listenUrl);
      this._sharpBoss.Run ();
      this._client = new RestClient (listenUrl);
    }

    [Test]
    public void TestGetRequest () {
      var url = string.Format ("/{0}/api/test?param0=A&param1=B", this._app);
      var response = this._client.Get (new RestRequest (url));

      Assert.NotNull (response);
      Assert.AreEqual (HttpStatusCode.OK, response.StatusCode);
    }

    [TearDown]
    public void RemoveAssembly () {
      this._sharpBoss.Stop ();
      var path = Path.GetFullPath (this._assembly.Location);
      var fileName = Path.GetFileName (path);

      Logger.Info (string.Format ("Removing WebServer from file {0} from {1}", fileName, path));
      File.Delete (path);
    }
  }
}