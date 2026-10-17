using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TraceX.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddProblemDetails();
builder.Services.AddHttpClient();


// Configure TraceX from appsettings.json
builder.Services.AddTraceX(options =>
{
    // Real keys should be loaded from configuration. Using a demo key here.
    options.ProjectKey = builder.Configuration["TraceX:ProjectKey"] ?? "tracex_live_demo_sample";
    options.Endpoint = builder.Configuration["TraceX:Endpoint"] ?? "http://localhost:5000/api/v1/";
    options.EnableLogging = true;
    options.DisableGzip = true; // Backend currently does not support gzip decompression
    options.Environment = builder.Environment.EnvironmentName;
});

var app = builder.Build();

// Enable TraceX Middleware to intercept unhandled exceptions and requests.
app.UseTraceX();

// removed swagger usages

app.UseAuthorization();

app.MapControllers();

app.Run();
