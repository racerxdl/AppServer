using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Server = global::SharpBoss.SharpBoss;

namespace SharpBoss.Tests;

[TestFixture]
[NonParallelizable]
public sealed class SharpBossTest
{
    [Test]
    public void ResolvesAndNormalizesListenUrlByDocumentedPrecedence()
    {
        const string settingName = "SHARPBOSS_URL";
        var previousEnvironmentValue = Environment.GetEnvironmentVariable(settingName);
        var roots = new List<string>();
        var settings = new Dictionary<string, string>
        {
            [settingName] = "http://localhost:31002",
        };

        try
        {
            Environment.SetEnvironmentVariable(settingName, "http://localhost:31001");

            using (var explicitServer = new Server(
                       "http://localhost:31003",
                       settings,
                       CreateOptions(roots)))
            {
                Assert.That(explicitServer.GetHttpServerListenUrl(), Is.EqualTo("http://localhost:31003/"));
            }

            using (var environmentServer = new Server(appSettings: settings, options: CreateOptions(roots)))
            {
                Assert.That(environmentServer.GetHttpServerListenUrl(), Is.EqualTo("http://localhost:31001/"));
            }

            Environment.SetEnvironmentVariable(settingName, null);

            using (var settingsServer = new Server(appSettings: settings, options: CreateOptions(roots)))
            {
                Assert.That(settingsServer.GetHttpServerListenUrl(), Is.EqualTo("http://localhost:31002/"));
            }

            using (var defaultServer = new Server(options: CreateOptions(roots)))
            {
                Assert.That(defaultServer.GetHttpServerListenUrl(), Is.EqualTo("http://localhost:8080/"));
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(settingName, previousEnvironmentValue);
            foreach (var root in roots)
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }
    }

    private static SharpBossOptions CreateOptions(ICollection<string> roots)
    {
        var root = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "configuration",
            Guid.NewGuid().ToString("N"));
        roots.Add(root);

        return new SharpBossOptions
        {
            ApplicationsDirectory = Path.Combine(root, "apps"),
            DeploymentDirectory = Path.Combine(root, "deployed"),
            ReloadDebounce = TimeSpan.FromSeconds(1),
        };
    }
}
