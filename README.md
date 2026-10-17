# TraceX .NET & ASP.NET Core SDK

Official .NET SDK for [TraceX](https://tracex.dev) — real-time crash monitoring, forensic telemetry, and incident triage.

## Installation

```bash
dotnet add package TraceX.AspNetCore
```

## Quick Start

In your ASP.NET Core `Program.cs`:

```csharp
using TraceX.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Register TraceX
builder.Services.AddTraceX(options =>
{
    options.ProjectKey = builder.Configuration["TraceX:ProjectKey"] ?? "tracex_live_your_project_key";
    options.Environment = builder.Environment.EnvironmentName;
    options.EnableLogging = true;
});

var app = builder.Build();

// Enable TraceX exception handling
app.UseExceptionHandler();

app.MapGet("/", () => "Hello TraceX!");

app.Run();
```

## Configuration Options

Configure in `appsettings.json`:

```json
{
  "TraceX": {
    "ProjectKey": "tracex_live_your_project_key",
    "Environment": "Production",
    "Endpoint": "https://tracex-api.kareemadel.com/api/v1/"
  }
}
```

## Features

- **Unhandled Exception Capture**: Captures crashes and call-stack frames across ASP.NET Core request pipelines.
- **Diagnostics & Breadcrumbs**: Automatic tracking of request context, HTTP method, and response status.
- **Asynchronous Non-Blocking Dispatch**: Telemetry is serialized and dispatched without adding latency to user requests.
- **Multi-Target**: Supports `.NET 8.0` and `.NET 9.0`.

## License

MIT License. See [LICENSE](LICENSE) for details.
