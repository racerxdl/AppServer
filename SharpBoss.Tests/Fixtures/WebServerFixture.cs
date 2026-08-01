using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using NUnit.Framework;
using Server = global::SharpBoss.SharpBoss;

namespace SharpBoss.Tests.Fixtures;

internal sealed class WebServerFixture : IAsyncDisposable
{
    private const string TestApplicationName = "testapp";
    private static readonly Lazy<IReadOnlyList<MetadataReference>> CompilationReferences =
        new(CreateCompilationReferences);

    private readonly string _applicationDirectory;
    private readonly HttpClient _client;
    private readonly string _deploymentDirectory;
    private readonly string _rootDirectory;
    private readonly string _listenUrl;
    private Server? _server;
    private Task? _runTask;

    private WebServerFixture()
    {
        _rootDirectory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "runtime",
            Guid.NewGuid().ToString("N"));
        _applicationDirectory = Path.Combine(_rootDirectory, "apps", TestApplicationName);
        _deploymentDirectory = Path.Combine(_rootDirectory, "deployed");
        Directory.CreateDirectory(_applicationDirectory);

        _listenUrl = $"http://127.0.0.1:{ReserveTcpPort()}/";
        _client = new HttpClient
        {
            BaseAddress = new Uri(_listenUrl),
            Timeout = TimeSpan.FromSeconds(5),
        };
    }

    public HttpClient Client => _client;

    public Server Server => _server ?? throw new InvalidOperationException("The fixture has not started.");

    public Task RunTask => _runTask ?? throw new InvalidOperationException("The fixture has not started.");

    public static async Task<WebServerFixture> StartAsync(string version = "v1")
    {
        var fixture = new WebServerFixture();

        try
        {
            fixture.CompileApplication(version);
            fixture._server = new Server(
                fixture._listenUrl,
                options: new SharpBossOptions
                {
                    ApplicationsDirectory = Path.GetDirectoryName(fixture._applicationDirectory)!,
                    DeploymentDirectory = fixture._deploymentDirectory,
                    ReloadDebounce = TimeSpan.FromMinutes(1),
                });
            fixture._runTask = fixture._server.RunAsync();
            await fixture.WaitUntilReadyAsync(version).ConfigureAwait(false);
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public void CompileApplication(string version, bool includeInvalidConverter = false)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            BuildApplicationSource(version, includeInvalidConverter),
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create(
            "TestApplication",
            [syntaxTree],
            CompilationReferences.Value,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                nullableContextOptions: NullableContextOptions.Enable));
        var assemblyPath = Path.Combine(_applicationDirectory, "TestApplication.dll");
        var temporaryPath = assemblyPath + "." + Guid.NewGuid().ToString("N") + ".new";

        try
        {
            EmitResult emitResult;
            using (var output = File.Create(temporaryPath))
            {
                emitResult = compilation.Emit(output);
            }

            if (!emitResult.Success)
            {
                var diagnostics = string.Join(
                    Environment.NewLine,
                    emitResult.Diagnostics
                        .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                        .Select(diagnostic => diagnostic.ToString()));
                throw new InvalidOperationException($"Could not compile the test application:{Environment.NewLine}{diagnostics}");
            }

            File.Move(temporaryPath, assemblyPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_server is not null)
            {
                await _server.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _client.Dispose();
            await CollectRetiredLoadContextAsync().ConfigureAwait(false);
            await DeleteRootDirectoryAsync().ConfigureAwait(false);
        }
    }

    private async Task WaitUntilReadyAsync(string expectedVersion)
    {
        var timeout = Stopwatch.StartNew();

        while (timeout.Elapsed < TimeSpan.FromSeconds(10))
        {
            if (_runTask?.IsFaulted == true)
            {
                await _runTask.ConfigureAwait(false);
            }

            try
            {
                using var response = await _client
                    .GetAsync($"/{TestApplicationName}/api/version")
                    .ConfigureAwait(false);
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.OK && content == expectedVersion)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25)).ConfigureAwait(false);
        }

        throw new TimeoutException($"SharpBoss did not become ready at '{_listenUrl}'.");
    }

    private async Task CollectRetiredLoadContextAsync()
    {
        var applicationDeploymentDirectory = Path.Combine(_deploymentDirectory, TestApplicationName);

        for (var attempt = 0; attempt < 40; attempt++)
        {
            ForceGarbageCollection();
            _server?.CleanupRetiredApplications();
            if (!Directory.Exists(applicationDeploymentDirectory))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25)).ConfigureAwait(false);
        }
    }

    private async Task DeleteRootDirectoryAsync()
    {
        Exception? lastError = null;

        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                if (Directory.Exists(_rootDirectory))
                {
                    Directory.Delete(_rootDirectory, recursive: true);
                }

                return;
            }
            catch (IOException exception)
            {
                lastError = exception;
            }
            catch (UnauthorizedAccessException exception)
            {
                lastError = exception;
            }

            ForceGarbageCollection();
            await Task.Delay(TimeSpan.FromMilliseconds(25)).ConfigureAwait(false);
        }

        TestContext.Progress.WriteLine(
            $"Deferred cleanup of '{_rootDirectory}' until the test process exits: {lastError?.Message}");
    }

    private static IReadOnlyList<MetadataReference> CreateCompilationReferences()
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string
            ?? throw new InvalidOperationException("The runtime did not expose trusted platform assemblies.");
        var pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        return trustedPlatformAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Append(typeof(Server).Assembly.Location)
            .Distinct(pathComparer)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }

    private static int ReserveTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static void ForceGarbageCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static string BuildApplicationSource(string version, bool includeInvalidConverter)
    {
        var versionLiteral = SymbolDisplay.FormatLiteral(version, quote: true);
        var invalidConverter = includeInvalidConverter
            ? """
              [RestJsonConverter]
              public abstract class InvalidMarkedConverter : JsonConverter<Token>
              {
              }
              """
            : string.Empty;

        return $$"""
            using System;
            using System.Globalization;
            using System.Net;
            using System.Text.Json;
            using System.Text.Json.Serialization;
            using System.Threading;
            using SharpBoss;
            using SharpBoss.Attributes;
            using SharpBoss.Attributes.Methods;
            using SharpBoss.Models;

            namespace TestApplication;

            public sealed record Token(int Value);

            public sealed record RequestMetadata(
                string Host,
                string[] Accept,
                string[] Languages,
                string Cookie,
                string? Query);

            public sealed class SharedCounter
            {
                public int Value { get; set; }
            }

            [RestJsonConverter]
            public sealed class TokenConverter : JsonConverter<Token>
            {
                public override Token Read(
                    ref Utf8JsonReader reader,
                    Type typeToConvert,
                    JsonSerializerOptions options)
                {
                    var text = reader.GetString();
                    if (text is null
                        || !text.StartsWith("token:", StringComparison.Ordinal)
                        || !int.TryParse(text[6..], NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                    {
                        throw new JsonException("Expected token:<number>.");
                    }

                    return new Token(value);
                }

                public override void Write(Utf8JsonWriter writer, Token value, JsonSerializerOptions options)
                {
                    writer.WriteStringValue($"token:{value.Value}");
                }
            }

            public abstract class UnmarkedConverter : JsonConverter<Token>
            {
            }

            {{invalidConverter}}

            public sealed class KnownApplicationException : Exception
            {
                public KnownApplicationException(string message)
                    : base(message)
                {
                }
            }

            [RestExceptionHandler(typeof(KnownApplicationException))]
            public sealed class KnownApplicationExceptionHandler : IRestExceptionHandler
            {
                public RestResponse HandleException(Exception exception)
                {
                    return new RestResponse(exception.Message, "text/plain", (HttpStatusCode)418);
                }
            }

            [REST("/api")]
            public sealed class PrimaryEndpoint
            {
                [Inject]
                private SharedCounter _counter = null!;

                [GET("/version")]
                public string Version()
                {
                    Thread.Sleep(10);
                    return {{versionLiteral}};
                }

                [GET("/metadata")]
                public RequestMetadata Metadata(RestRequest request)
                {
                    return new RequestMetadata(
                        request.UserHostName,
                        request.AcceptTypes,
                        request.UserLanguages,
                        request.Cookies["session"]?.Value ?? string.Empty,
                        request.QueryString["name"]);
                }

                [POST("/convert")]
                public Token Convert(Token value)
                {
                    return value;
                }

                [GET("/handled")]
                public string Handled()
                {
                    throw new KnownApplicationException("handled detail");
                }

                [GET("/unhandled")]
                public string Unhandled()
                {
                    throw new InvalidOperationException("sensitive endpoint detail");
                }

                [GET("/counter")]
                public string IncrementCounter()
                {
                    _counter.Value++;
                    return _counter.Value.ToString(CultureInfo.InvariantCulture);
                }
            }

            [REST("/api")]
            public sealed class SecondaryEndpoint
            {
                [Inject]
                private SharedCounter _counter = null!;

                [GET("/counter-shared")]
                public string ReadCounter()
                {
                    return _counter.Value.ToString(CultureInfo.InvariantCulture);
                }
            }
            """;
    }
}
