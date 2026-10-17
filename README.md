# TraceX .NET & ASP.NET Core SDK

Official .NET SDK for [TraceX](https://tracex.dev) — real-time crash monitoring, forensic telemetry, and incident triage.

## Features

- **Global Error Handling**: Automatically catches unhandled exceptions in ASP.NET Core controllers, minimal APIs, background workers, and AppDomain events.
- **Breadcrumb Harvesting**: Circular ring buffer tracks up to 50 user actions, routing, and HTTP requests leading up to a crash.
- **Offline Resilience**: Memory/Disk fallback buffer queues up to 100 crashes when network is disconnected or backend returns HTTP 503.
- **Compression**: Automatic Gzip compression for payloads > 1KB.
- **Data Sanitization**: Scrubber for sensitive data like HTTP Bearer tokens.
- **Context Enrichment**: Attaches OS, Process Memory, and App Version metadata.
- **Multi-Target**: Supports `.NET 8.0` and `.NET 9.0`.

## Installation

Install the NuGet package (for ASP.NET Core apps):

```bash
dotnet add package TraceX.AspNetCore
```

Or for standalone .NET libraries and console applications:

```bash
dotnet add package TraceX.Core
```

## Quick Start (ASP.NET Core)

In your ASP.NET Core `Program.cs`:

```csharp
using TraceX.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Register TraceX services
builder.Services.AddTraceX(options =>
{
    // Required: Your unique TraceX project key
    options.ProjectKey = builder.Configuration["TraceX:ProjectKey"] ?? "tracex_live_your_project_key";
    
    // Optional configuration
    options.Endpoint = builder.Configuration["TraceX:Endpoint"] ?? "https://tracex-api.kareemadel.com/api/v1/";
    options.Environment = builder.Environment.EnvironmentName;
    options.EnableLogging = true;
    options.DisableGzip = false; // Set to true if backend does not support gzip decompression
});

var app = builder.Build();

// Enable TraceX exception handling middleware
app.UseTraceX();

app.MapGet("/", () => "Hello TraceX!");

app.Run();
```

## Configuration Reference

Add this section to your `appsettings.json`:

```json
{
  "TraceX": {
    "ProjectKey": "tracex_live_your_project_key",
    "Environment": "Production",
    "Endpoint": "https://tracex-api.kareemadel.com/api/v1/"
  }
}
```

## TraceX.Samples.Api

This repository contains `TraceX.Samples.Api`, a Web API project with intentional crash endpoints to simulate testing. 
Run it via:
```bash
cd TraceX.Samples.Api
dotnet run
```

## License

MIT License. See [LICENSE](LICENSE) for details.
