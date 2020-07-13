using System;
using NUnit.Framework;
using SharpBoss.Tests.Fixtures;

namespace SharpBoss.Tests {
  public class SharpBossTest {
    private SharpBoss _sharpBoss;

    [Test]
    public void TestDefaultListenUrl () {
      this._sharpBoss = new SharpBoss ();

      Assert.NotNull (this._sharpBoss);
      Assert.NotNull (this._sharpBoss.GetHttpServerListenUrl ());
    }

    [Test]
    public void TestListenUrlWithSlash () {
      var listenUrl = "http://localhost:1234/";
      this._sharpBoss = new SharpBoss (listenUrl);

      Assert.NotNull (this._sharpBoss);
      Assert.AreEqual (listenUrl, this._sharpBoss.GetHttpServerListenUrl ());
    }

    [Test]
    public void TestListenUrlWithoutSlash () {
      var listenUrl = "http://localhost:4321";
      this._sharpBoss = new SharpBoss (listenUrl);

      Assert.NotNull (this._sharpBoss);
      Assert.AreEqual (listenUrl + "/", this._sharpBoss.GetHttpServerListenUrl ());
    }

    [Test]
    public void TestListenUrlFromEnvironmentWithSlash () {
      var listenUrl = "http://localhost:4444/";
      Environment.SetEnvironmentVariable ("SHARPBOSS_URL", listenUrl);

      this._sharpBoss = new SharpBoss ();

      Assert.NotNull (this._sharpBoss);
      Assert.AreEqual (listenUrl, this._sharpBoss.GetHttpServerListenUrl ());
    }

    [Test]
    public void TestListenUrlFromEnvironmentWithoutSlash () {
      var listenUrl = "http://localhost:3145";
      Environment.SetEnvironmentVariable ("SHARPBOSS_URL", listenUrl);

      this._sharpBoss = new SharpBoss ();

      Assert.NotNull (this._sharpBoss);
      Assert.AreEqual (listenUrl + "/", this._sharpBoss.GetHttpServerListenUrl ());
    }

    [Test]
    public void TestListenUrlFromConfigWithSlash () {
      var listenUrl = "http://localhost:9124/";
      var appSettings = WebServerFixture.CreateConfigFile (listenUrl);

      this._sharpBoss = new SharpBoss (appSettings: appSettings);

      Assert.NotNull (this._sharpBoss);
      Assert.AreEqual (listenUrl, this._sharpBoss.GetHttpServerListenUrl ());
    }

    [Test]
    public void TestListenUrlFromConfigWithoutSlash () {
      var listenUrl = "http://localhost:2122";
      var appSettings = WebServerFixture.CreateConfigFile (listenUrl);

      this._sharpBoss = new SharpBoss (appSettings: appSettings);

      Assert.NotNull (this._sharpBoss);
      Assert.AreEqual (listenUrl + "/", this._sharpBoss.GetHttpServerListenUrl ());
    }
  }
}