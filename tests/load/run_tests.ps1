if (-not $env:TRACEX_PROJECT_KEY) {
    Write-Error "TRACEX_PROJECT_KEY environment variable is required"
    exit 1
}

$env:LOCUST_THROUGHPUT_PER_VU="0.1666"
echo "Starting baseline test (500 RPM)..."
locust -f tests/load/locustfile.py --headless --users 50 --spawn-rate 50 --run-time 2m --host https://tracex-api.kareemadel.com --csv=tests/load/baseline_results

$env:LOCUST_THROUGHPUT_PER_VU="0.6666"
echo "Starting burst test (2000 RPM)..."
locust -f tests/load/locustfile.py --headless --users 50 --spawn-rate 50 --run-time 15m --host https://tracex-api.kareemadel.com --csv=tests/load/burst_results
