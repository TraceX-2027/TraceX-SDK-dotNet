import os
from datetime import datetime, timezone
from locust import HttpUser, task, constant_throughput, events

server_latencies = []

def percentile(data, p):
    if not data:
        return 0
    data = sorted(data)
    n = len(data)
    k = (n - 1) * p / 100.0
    f = int(k)
    c = f + 1
    if c >= n:
        return data[-1]
    return data[f] + (k - f) * (data[c] - data[f])

@events.test_stop.add_listener
def on_test_stop(environment, **kwargs):
    if len(server_latencies) > 0:
        p50 = percentile(server_latencies, 50)
        p95 = percentile(server_latencies, 95)
        p99 = percentile(server_latencies, 99)
        print(f"\n--- Server-Side Processing Latency (ms) ---")
        print(f"Count: {len(server_latencies)}")
        print(f"p50:   {p50:.2f}")
        print(f"p95:   {p95:.2f}")
        print(f"p99:   {p99:.2f}")
        print(f"-------------------------------------------\n")

class ValidCrashUser(HttpUser):
    wait_time = constant_throughput(float(os.environ.get("LOCUST_THROUGHPUT_PER_VU", "0.666")))

    def on_start(self):
        self.project_key = os.environ.get("TRACEX_PROJECT_KEY")
        if not self.project_key:
            raise Exception("TRACEX_PROJECT_KEY environment variable is required")

    @task
    def valid_crash_report(self):
        payload = {
            "project_key": self.project_key,
            "platform": "dotnet",
            "language": "csharp",
            "occurred_at": datetime.now(timezone.utc).isoformat(),
            "exception_type": "System.NullReferenceException",
            "error_message": "Object reference not set to an instance of an object. (Load Test)",
            "stack_trace": "   at TraceX.LoadTest.SimulateCrash() in /app/LoadTest.cs:line 42\n   at TraceX.LoadTest.Execute() in /app/LoadTest.cs:line 20",
            "environment": {
                "app_version": "1.0.0-loadtest",
                "device_model": "Locust Load Generator",
                "runtime_version": ".NET 9.0.0",
                "os_name": "Linux",
                "os_version": "Ubuntu 22.04",
                "hostname": "locust-worker-1",
                "process_memory_bytes": 1024000,
                "environment_name": "LoadTest"
            },
            "breadcrumbs": [
                {
                    "sequence_order": 1,
                    "timestamp": datetime.now(timezone.utc).isoformat(),
                    "category": "system",
                    "action": "load_test_start",
                    "target": "Locust",
                    "data": { "vu_id": str(id(self)) }
                }
            ]
        }

        headers = {
            "Content-Type": "application/json",
            "X-TraceX-Key": self.project_key
        }

        with self.client.post("/api/v1/crashes", json=payload, headers=headers, catch_response=True, timeout=10, name="/api/v1/crashes [valid]") as response:
            server_latency = None
            try:
                if "x-response-time-ms" in response.headers:
                    server_latency = float(response.headers["x-response-time-ms"])
            except (ValueError, TypeError):
                server_latency = None

            if response.status_code == 202:
                response.success()
                if server_latency is not None:
                    server_latencies.append(server_latency)
            elif response.status_code == 429:
                if "Retry-After" in response.headers:
                    response.success()
                else:
                    response.failure("429 missing Retry-After header")
            else:
                response.failure(f"Unexpected status code {response.status_code}")

class InvalidKeyUser(HttpUser):
    wait_time = constant_throughput(1.0)

    def on_start(self):
        self.invalid_key = "invalid_key_for_testing"

    @task
    def invalid_project_key(self):
        payload = {
            "project_key": self.invalid_key,
            "platform": "dotnet",
            "language": "csharp",
            "occurred_at": datetime.now(timezone.utc).isoformat(),
            "exception_type": "System.UnauthorizedAccessException",
            "error_message": "Invalid key test",
            "stack_trace": "...",
            "environment": {
                "app_version": "1.0.0",
                "device_model": "Locust",
                "os_name": "Linux",
                "os_version": "Ubuntu"
            },
            "breadcrumbs": []
        }

        headers = {
            "Content-Type": "application/json",
            "X-TraceX-Key": self.invalid_key
        }

        with self.client.post("/api/v1/crashes", json=payload, headers=headers, catch_response=True, timeout=10, name="/api/v1/crashes [invalid_key]") as response:
            if response.status_code == 401:
                response.success()
            else:
                response.failure(f"Expected 401 Unauthorized, got {response.status_code}")
