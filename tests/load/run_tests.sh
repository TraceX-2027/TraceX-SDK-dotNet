#!/usr/bin/env bash
set -euo pipefail

if [ -z "${TRACEX_PROJECT_KEY:-}" ]; then
    echo "Error: TRACEX_PROJECT_KEY environment variable is required"
    exit 1
fi

HOST_URL="${TRACEX_API_HOST:-https://tracex-api.kareemadel.com}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOCUST_FILE="$SCRIPT_DIR/locustfile.py"
REPORTS_DIR="$SCRIPT_DIR/reports"

mkdir -p "$REPORTS_DIR"

echo "=== TraceX Load Tests ==="
echo "Target Host: $HOST_URL"

echo -e "\n[1/3] Starting Invalid Key Validation Test..."
locust -f "$LOCUST_FILE" InvalidKeyUser --headless --users 5 --spawn-rate 5 --run-time 10s --host "$HOST_URL" --csv="$REPORTS_DIR/invalid_key"

echo -e "\n[2/3] Starting Baseline Test (500 RPM for 2m)..."
export LOCUST_THROUGHPUT_PER_VU="0.1666"
locust -f "$LOCUST_FILE" ValidCrashUser --headless --users 50 --spawn-rate 50 --run-time 2m --host "$HOST_URL" --csv="$REPORTS_DIR/baseline"

echo -e "\n[3/3] Starting Burst Test (2000 RPM for 15m)..."
export LOCUST_THROUGHPUT_PER_VU="0.6666"
locust -f "$LOCUST_FILE" ValidCrashUser --headless --users 50 --spawn-rate 50 --run-time 15m --host "$HOST_URL" --csv="$REPORTS_DIR/burst"

echo -e "\nAll tests completed successfully."
exit 0
