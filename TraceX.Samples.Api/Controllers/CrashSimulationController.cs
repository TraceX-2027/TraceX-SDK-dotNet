using Microsoft.AspNetCore.Mvc;

namespace TraceX.Samples.Api.Controllers;

[ApiController]
[Route("api")]
public class CrashSimulationController : ControllerBase
{
    private readonly HttpClient _httpClient;

    public CrashSimulationController(IHttpClientFactory httpClientFactory)
    {
        // Using an HTTP client to demonstrate dependency instrumentation and breadcrumbs
        _httpClient = httpClientFactory.CreateClient();
    }

    [HttpGet("null-reference")]
    public IActionResult TriggerNullReference()
    {
        string? testString = null;
        // This will intentionally throw a NullReferenceException, which UseTraceX middleware will capture
        int length = testString!.Length; 
        
        return Ok(length);
    }

    [HttpGet("http-dependency")]
    public async Task<IActionResult> TriggerHttpCrash()
    {
        // This HTTP request will be recorded as a Breadcrumb by the TraceX diagnostic listener
        await _httpClient.GetAsync("https://example.com/api/test");

        throw new InvalidOperationException("This exception occurred after making an HTTP request. Check your breadcrumbs!");
    }

    [HttpGet("background-worker")]
    public IActionResult TriggerBackgroundCrash()
    {
        var thread = new Thread(() =>
        {
            Thread.Sleep(100);
            throw new ArgumentOutOfRangeException("This unobserved exception should be caught by the AppDomain hook deterministically.");
        });
        thread.Start();

        return Accepted(new { message = "Background thread started. It will crash shortly." });
    }
}
