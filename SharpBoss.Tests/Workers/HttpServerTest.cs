using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using SharpBoss.Tests.Fixtures;

namespace SharpBoss.Tests.Workers;

[TestFixture]
[NonParallelizable]
public sealed class HttpServerTest
{
    [Test]
    public async Task ServesRequestsAndPreservesRequestMetadata()
    {
        await using var fixture = await WebServerFixture.StartAsync().ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/testapp/api/metadata?name=alpha");
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.AcceptLanguage.ParseAdd("en-US");
        request.Headers.Add("Cookie", "session=abc");

        using var response = await fixture.Client.SendAsync(request).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var document = JsonDocument.Parse(content);
        var metadata = document.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(metadata.GetProperty("Host").GetString(), Is.EqualTo(fixture.Client.BaseAddress!.Authority));
            Assert.That(metadata.GetProperty("Accept")[0].GetString(), Is.EqualTo("application/json"));
            Assert.That(metadata.GetProperty("Languages")[0].GetString(), Is.EqualTo("en-US"));
            Assert.That(metadata.GetProperty("Cookie").GetString(), Is.EqualTo("abc"));
            Assert.That(metadata.GetProperty("Query").GetString(), Is.EqualTo("alpha"));
        });
    }

    [Test]
    public async Task AppliesExplicitJsonConverterToRequestAndResponse()
    {
        await using var fixture = await WebServerFixture.StartAsync().ConfigureAwait(false);
        using var requestContent = new StringContent("\"token:17\"", Encoding.UTF8, "application/json");

        using var response = await fixture.Client
            .PostAsync("/testapp/api/convert", requestContent)
            .ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
            Assert.That(content, Is.EqualTo("\"token:17\""));
        });

        fixture.CompileApplication("v2");
        fixture.Server.ForceReload();
        var retiredLoadContext = fixture.Server.LastRetiredLoadContext;
        Assert.That(retiredLoadContext, Is.Not.Null);
        await AssertLoadContextCollectedAsync(fixture, retiredLoadContext!).ConfigureAwait(false);
    }

    [Test]
    public async Task SharesInjectedServicesAndUsesRegisteredExceptionHandler()
    {
        await using var fixture = await WebServerFixture.StartAsync().ConfigureAwait(false);

        var incremented = await fixture.Client
            .GetStringAsync("/testapp/api/counter")
            .ConfigureAwait(false);
        var shared = await fixture.Client
            .GetStringAsync("/testapp/api/counter-shared")
            .ConfigureAwait(false);
        using var handledResponse = await fixture.Client
            .GetAsync("/testapp/api/handled")
            .ConfigureAwait(false);
        var handledContent = await handledResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(incremented, Is.EqualTo("1"));
            Assert.That(shared, Is.EqualTo("1"));
            Assert.That(handledResponse.StatusCode, Is.EqualTo((HttpStatusCode)418));
            Assert.That(handledContent, Is.EqualTo("handled detail"));
        });
    }

    [Test]
    public async Task SanitizesUnhandledEndpointErrors()
    {
        await using var fixture = await WebServerFixture.StartAsync().ConfigureAwait(false);

        using var response = await fixture.Client
            .GetAsync("/testapp/api/unhandled")
            .ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(content, Does.StartWith("Internal server error. Reference: "));
            Assert.That(content, Does.Not.Contain("sensitive endpoint detail"));
            Assert.That(content, Does.Not.Contain(nameof(InvalidOperationException)));
            Assert.That(content, Does.Not.Contain(" at "));
        });
    }

    [Test]
    public async Task StopObservesServerLifetimeAndPreventsRestart()
    {
        await using var fixture = await WebServerFixture.StartAsync().ConfigureAwait(false);

        await fixture.Server.StopAsync().ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(fixture.RunTask.IsCompleted, Is.True);
            Assert.That(fixture.RunTask.IsFaulted, Is.False);
            Assert.That(
                () => fixture.Server.Run(),
                Throws.TypeOf<InvalidOperationException>());
        });
    }

    [Test]
    public async Task ReloadIsAtomicAndCollectsPreviousLoadContext()
    {
        await using var fixture = await WebServerFixture.StartAsync("v1").ConfigureAwait(false);
        var initial = await GetVersionAsync(fixture).ConfigureAwait(false);
        fixture.CompileApplication("v2");

        var firstBatch = Enumerable.Range(0, 32)
            .Select(_ => GetVersionAsync(fixture))
            .ToArray();
        await Task.Delay(TimeSpan.FromMilliseconds(5)).ConfigureAwait(false);
        var reloadTask = Task.Run(fixture.Server.ForceReload);
        var secondBatch = Enumerable.Range(0, 32)
            .Select(_ => GetVersionAsync(fixture))
            .ToArray();
        var concurrentResults = await Task.WhenAll(firstBatch.Concat(secondBatch)).ConfigureAwait(false);
        await reloadTask.ConfigureAwait(false);

        var reloaded = await GetVersionAsync(fixture).ConfigureAwait(false);
        var retiredLoadContext = fixture.Server.LastRetiredLoadContext;

        Assert.Multiple(() =>
        {
            Assert.That(initial.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(initial.Content, Is.EqualTo("v1"));
            Assert.That(concurrentResults, Has.All.Matches<HttpResult>(result =>
                result.StatusCode == HttpStatusCode.OK && result.Content is "v1" or "v2"));
            Assert.That(reloaded.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(reloaded.Content, Is.EqualTo("v2"));
            Assert.That(retiredLoadContext, Is.Not.Null);
        });

        await AssertLoadContextCollectedAsync(fixture, retiredLoadContext!).ConfigureAwait(false);
    }

    [Test]
    public async Task RejectedReloadKeepsPreviousGenerationActive()
    {
        await using var fixture = await WebServerFixture.StartAsync("v1").ConfigureAwait(false);
        fixture.CompileApplication("v2", includeInvalidConverter: true);

        var reloadError = Assert.Throws<AggregateException>(fixture.Server.ForceReload);
        var current = await GetVersionAsync(fixture).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(reloadError, Is.Not.Null);
            Assert.That(reloadError!.ToString(), Does.Contain("must be concrete and closed"));
            Assert.That(current.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(current.Content, Is.EqualTo("v1"));
        });
    }

    private static async Task<HttpResult> GetVersionAsync(WebServerFixture fixture)
    {
        using var response = await fixture.Client
            .GetAsync("/testapp/api/version")
            .ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return new HttpResult(response.StatusCode, content);
    }

    private static async Task AssertLoadContextCollectedAsync(
        WebServerFixture fixture,
        WeakReference loadContext)
    {
        for (var attempt = 0; attempt < 40 && loadContext.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            fixture.Server.CleanupRetiredApplications();
            await Task.Delay(TimeSpan.FromMilliseconds(25)).ConfigureAwait(false);
        }

        Assert.That(loadContext.IsAlive, Is.False, "The retired collectible AssemblyLoadContext remained rooted.");
    }

    private sealed record HttpResult(HttpStatusCode StatusCode, string Content);
}
