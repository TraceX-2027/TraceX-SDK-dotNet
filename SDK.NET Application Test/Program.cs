using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TraceX.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddProblemDetails();

// Configure TraceX SDK
builder.Services.AddTraceX(options =>
{
    options.ProjectKey = builder.Configuration["TraceX:ProjectKey"] ?? "tracex_live_demo_gui_test";
    options.Endpoint = builder.Configuration["TraceX:Endpoint"] ?? "https://tracex-api.kareemadel.com/api/v1/";
    options.EnableLogging = true;
    options.Environment = builder.Environment.EnvironmentName;
});

var app = builder.Build();

// Enable TraceX Middleware to intercept unhandled exceptions
app.UseTraceX();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
