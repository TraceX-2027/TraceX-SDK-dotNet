using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TraceX.Core;

namespace TraceX.Samples.WebApp.Pages;

[IgnoreAntiforgeryToken]
public class IndexModel : PageModel
{
    // Minimal app-level state to display the UI's test history (as requested)
    public static List<TraceXBreadcrumb> UIBreadcrumbHistory { get; } = new();
    public static CrashInfo? UILastCrash { get; set; }

    public class CrashInfo
    {
        public DateTime Timestamp { get; set; }
        public string ExceptionType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    public IActionResult OnGetState()
    {
        return new JsonResult(new { 
            breadcrumbs = UIBreadcrumbHistory, 
            lastCrash = UILastCrash 
        });
    }

    public IActionResult OnPostAddBreadcrumb()
    {
        var bc = new TraceXBreadcrumb
        {
            Timestamp = DateTime.UtcNow,
            Type = "application-test",
            Message = "button-click",
            Data = new Dictionary<string, string>
            {
                { "target", "Add Test Breadcrumb" },
                { "action", "button-click" }
            }
        };

        // Track in UI state
        UIBreadcrumbHistory.Add(bc);
        
        // Push to real SDK buffer to exercise the public API
        TraceXBreadcrumbScope.Current?.Add(bc);

        return new JsonResult(bc);
    }

    public IActionResult OnPostTriggerCrash()
    {
        // Replay the collected breadcrumbs into the SDK's actual current request scope.
        // This ensures the SDK captures the full timeline as if they happened within this single request flow.
        var scope = TraceXBreadcrumbScope.Current;
        if (scope != null)
        {
            foreach (var bc in UIBreadcrumbHistory)
            {
                scope.Add(bc);
            }

            scope.Add(new TraceXBreadcrumb
            {
                Timestamp = DateTime.UtcNow,
                Type = "application-test",
                Message = "crash-trigger",
                Data = new Dictionary<string, string>
                {
                    { "target", "Trigger Test Crash" },
                    { "action", "crash-trigger" }
                }
            });
        }

        UILastCrash = new CrashInfo
        {
            Timestamp = DateTime.UtcNow,
            ExceptionType = "InvalidOperationException",
            Message = "TraceX GUI application test crash"
        };

        // Intentional exception to test TraceX SDK middleware
        throw new InvalidOperationException(UILastCrash.Message);
    }

    public IActionResult OnPostClear()
    {
        UIBreadcrumbHistory.Clear();
        UILastCrash = null;
        return new JsonResult(new { success = true });
    }
}
