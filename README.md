# TraceX.SDK.DotNet

The official .NET client library for TraceX, an AI-powered crash monitoring and remediation platform.

## Features

- **Global Error Handling**: Automatically catches unhandled exceptions in ASP.NET Core controllers, minimal APIs, background workers, and AppDomain events.
- **Breadcrumb Harvesting**: Circular ring buffer tracks up to 50 user actions, routing, and HTTP requests leading up to a crash.
- **Offline Resilience**: Memory/Disk fallback buffer queues up to 100 crashes when network is disconnected or backend returns HTTP 503.
- **Compression**: Automatic Gzip compression for payloads > 1KB.
- **Data Sanitization**: Scrubber for sensitive data like HTTP Bearer tokens.
- **Context Enrichment**: Attaches OS, Process Memory, and App Version metadata.

## Installation

Install the NuGet package (for ASP.NET Core apps):

```bash
dotnet add package TraceX.AspNetCore
```

## Quickstart (ASP.NET Core)

Update your `Program.cs` to add TraceX services and middleware.

```csharp
using TraceX.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add TraceX Configuration
builder.Services.AddTraceX(options =>
{
    // Required: Your unique TraceX project key
    options.ProjectKey = builder.Configuration["TraceX:ProjectKey"];
    
    // Optional configuration
    options.Endpoint = builder.Configuration["TraceX:Endpoint"] ?? "https://api.tracex.dev/api/v1/";
    options.Environment = builder.Environment.EnvironmentName;
    options.EnableLogging = true;
    options.DisableGzip = false; // Set to true if backend does not support gzip decompression
});

var app = builder.Build();

// Enable Exception Interception
app.UseTraceX();

// ... Map controllers
app.Run();
```

### Configuration Reference

Add this section to your `appsettings.json`:

```json
{
  "TraceX": {
    "ProjectKey": "tracex_live_pub_98a72bc4",
    "Endpoint": "https://api.tracex.dev/api/v1/"
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
