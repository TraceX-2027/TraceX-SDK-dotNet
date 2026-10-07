$ErrorActionPreference = "Stop"

if (-not $env:TRACEX_PROJECT_KEY) {
    Write-Error "TRACEX_PROJECT_KEY environment variable is required"
    exit 1
}

$HostUrl = $env:TRACEX_API_HOST
if (-not $HostUrl) {
    $HostUrl = "https://tracex-api.kareemadel.com"
}

$ScriptDir = $PSScriptRoot
$LocustFile = Join-Path $ScriptDir "locustfile.py"
$ReportsDir = Join-Path $ScriptDir "reports"

if (-not (Test-Path $ReportsDir)) {
    New-Item -ItemType Directory -Path $ReportsDir | Out-Null
}

Write-Host "=== TraceX Load Tests ==="
Write-Host "Target Host: $HostUrl"

# 1. Validation Test: Invalid Key
Write-Host "`n[1/3] Starting Invalid Key Validation Test..."
locust -f $LocustFile InvalidKeyUser --headless --users 5 --spawn-rate 5 --run-time 10s --host $HostUrl --csv="$ReportsDir/invalid_key"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Invalid Key test failed (Exit code: $LASTEXITCODE)"
    exit $LASTEXITCODE
}

# 2. Baseline Test: Valid Crash (500 RPM -> ~8.3 req/s -> 50 users * 0.1666)
$env:LOCUST_THROUGHPUT_PER_VU="0.1666"
Write-Host "`n[2/3] Starting Baseline Test (500 RPM for 2m)..."
locust -f $LocustFile ValidCrashUser --headless --users 50 --spawn-rate 50 --run-time 2m --host $HostUrl --csv="$ReportsDir/baseline"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Baseline test failed (Exit code: $LASTEXITCODE)"
    exit $LASTEXITCODE
}

# 3. Burst Test: Valid Crash (2000 RPM -> ~33.3 req/s -> 50 users * 0.6666)
$env:LOCUST_THROUGHPUT_PER_VU="0.6666"
Write-Host "`n[3/3] Starting Burst Test (2000 RPM for 15m)..."
locust -f $LocustFile ValidCrashUser --headless --users 50 --spawn-rate 50 --run-time 15m --host $HostUrl --csv="$ReportsDir/burst"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Burst test failed (Exit code: $LASTEXITCODE)"
    exit $LASTEXITCODE
}

Write-Host "`nAll tests completed successfully."
exit 0
