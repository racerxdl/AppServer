# SharpBoss

[![.NET](https://github.com/racerxdl/AppServer/actions/workflows/dotnet-core.yml/badge.svg)](https://github.com/racerxdl/AppServer/actions/workflows/dotnet-core.yml)
[![GPLv3 License](https://img.shields.io/badge/license-GPLv3-brightgreen.svg)](https://tldrlegal.com/license/gnu-general-public-license-v3-(gpl-3))

SharpBoss is a cross-platform .NET 10 library for hosting multiple dynamically loaded HTTP applications.

## Features

- HTTP hosting through EmbedIO.
- Atomic hot deployment and removal of application assemblies.
- Collectible application load contexts with isolated JSON runtime state.
- Multiple independently named applications.
- Request body, query parameter, and `RestRequest` metadata binding.
- Explicit `System.Text.Json` converter registration.
- Shared field injection through `[Inject]`.
- Application-specific exception handlers.
- Sanitized unhandled-error responses with a request reference; full details remain in server logs.
- Owned asynchronous lifecycle and cancellation.

## Build and test

Install a .NET 10 SDK, then run:

```sh
dotnet build SharpBoss.sln --configuration Release
dotnet test SharpBoss.Tests/SharpBoss.Tests.csproj --configuration Release
```

`global.json` pins the supported SDK feature band and permits the latest patch in that band.

## Host SharpBoss

Reference `SharpBoss/SharpBoss.csproj` from a .NET 10 executable and own the server for its full lifetime:

```csharp
using SharpBoss;

var options = new SharpBossOptions
{
    ApplicationsDirectory = Path.Combine(AppContext.BaseDirectory, "apps"),
    DeploymentDirectory = Path.Combine(AppContext.BaseDirectory, "deployed"),
};

await using var server = new SharpBoss.SharpBoss(
    "http://localhost:8080/",
    options: options);

await server.RunAsync(shutdownToken);
```

The listen URL is resolved in this order:

1. The constructor's `listenUrl` argument.
2. The `SHARPBOSS_URL` environment variable.
3. The `SHARPBOSS_URL` entry in the constructor's `appSettings` dictionary.
4. `http://localhost:8080/`.

`StopAsync` cancels and observes the server lifetime task. A `SharpBoss` instance can be started once.

## Deploy an application

Build an application as a .NET 10 class library referencing SharpBoss. Put its managed assemblies and dependencies in one subdirectory:

```text
apps/
└── sampleapp/
    ├── SampleApp.dll
    └── SampleApp.pdb
```

The directory name is the application's URL prefix. SharpBoss copies each generation to a unique directory under `deployed`, validates it completely, and swaps it into service atomically. A failed reload leaves the previous generation active. File changes trigger a debounced reload; hosts can also call `ForceReload()`.

## Define endpoints

```csharp
using SharpBoss.Attributes;
using SharpBoss.Attributes.Methods;
using SharpBoss.Models;

namespace SampleApp;

public sealed class Counter
{
    public int Value { get; set; }
}

[REST("/api")]
public sealed class SampleEndpoint
{
    [Inject]
    private Counter _counter = null!;

    [GET("/hello")]
    public string Hello([QueryParam("name")] string name, RestRequest request)
    {
        _counter.Value++;
        return $"Hello {name} from {request.UserHostName}; call {_counter.Value}.";
    }

    [POST("/echo")]
    public Message Echo(Message message) => message;
}

public sealed record Message(string Text);
```

This application exposes:

- `GET /sampleapp/api/hello?name=Lucas`
- `POST /sampleapp/api/echo`

Strings are returned as `text/plain`. Other return values are serialized as `application/json`. Non-string parameters without `[QueryParam]` are deserialized from the request body. A `RestRequest` parameter receives immutable request metadata, including headers, cookies, query values, host, languages, remote endpoint, and body.

Fields marked `[Inject]` are instantiated once per application generation and shared by exact field type across endpoint classes.

## Register JSON converters

Converters are opt-in. Mark concrete, closed `JsonConverter` implementations that have a public parameterless constructor:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using SharpBoss.Attributes;

[RestJsonConverter]
public sealed class MessageConverter : JsonConverter<Message>
{
    public override Message Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) => new(reader.GetString() ?? string.Empty);

    public override void Write(
        Utf8JsonWriter writer,
        Message value,
        JsonSerializerOptions options) => writer.WriteStringValue(value.Text);
}
```

The same ordered converter set is used for request deserialization and response serialization. Use `[RestJsonConverter(order)]` when converter precedence matters. Unmarked converter types are ignored; invalid marked converters reject the new generation.

## Handle application exceptions

```csharp
using System.Net;
using SharpBoss;
using SharpBoss.Attributes;
using SharpBoss.Models;

public sealed class UnknownMessageException : Exception
{
    public UnknownMessageException(string message)
        : base(message)
    {
    }
}

[RestExceptionHandler(typeof(UnknownMessageException))]
public sealed class UnknownMessageHandler : IRestExceptionHandler
{
    public RestResponse HandleException(Exception exception) => new(
        exception.Message,
        "text/plain",
        HttpStatusCode.NotFound);
}
```

SharpBoss chooses the most specific registered handler while walking the exception's base types. Unhandled exceptions are logged and returned as HTTP 500 with only `Internal server error. Reference: <request-id>` in the response body.
