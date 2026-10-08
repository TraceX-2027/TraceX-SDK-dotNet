# TraceX SDK Sample Application (SDK.NET Application Test)

A minimal ASP.NET Core application designed specifically for validating the TraceX .NET SDK integration in a real-world scenario. It provides a professional UI dashboard to trigger and observe actual crashes, breadcrumbs, and asynchronous dispatch behavior.

## Dependencies & Setup

This test application is natively bundled within the TraceX-SDK-dotNet repository. It relies on local ProjectReference links to the adjacent TraceX.AspNetCore project. No NuGet packaging is required for local validation.

## How to Run the App and Perform the Test

**1. Configure API Key and Start the application:**
Do not hardcode secrets into the source code. Pass the real API key via environment variables.

From this directory (SDK.NET Application Test), run:

**PowerShell (Windows):**
``powershell
$env:TraceX__ProjectKey = "your_real_api_key_here"
dotnet run
``

**Bash (Linux/Mac):**
``bash
export TraceX__ProjectKey="your_real_api_key_here"
dotnet run
``

**2. Access the Application Test Dashboard:**
Open the application in your browser (usually http://localhost:5151 or the URL provided in your console output).

**3. Interact with the SDK:**
- **Verify Status**: The dashboard will explicitly show TraceX SDK: Initialized.
- **Add Breadcrumbs**: Click "Add Test Breadcrumb" to log timeline events locally into the SDK's scoped context.
- **Trigger Test Crash**: Click "Trigger Test Crash" to intentionally throw an InvalidOperationException.

**4. Validation:**
- The application will intercept the crash via TraceXMiddleware.
- The GUI intercepts the resulting HTTP 500 error and visually updates the "Last Test Crash" panel.
- The SDK's asynchronous background dispatcher will package the captured crash and breadcrumbs, and dispatch them to the TraceX backend. Verify receipt directly in your TraceX instance.
