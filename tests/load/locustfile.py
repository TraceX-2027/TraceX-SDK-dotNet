import os
import json
import time
import re
from datetime import datetime, timezone
from locust import HttpUser, task, constant_throughput

class TraceXLoadUser(HttpUser):
    wait_time = constant_throughput(float(os.environ.get("LOCUST_THROUGHPUT_PER_VU", "0.666")))

    def on_start(self):
        self.project_key = os.environ.get("TRACEX_PROJECT_KEY")
        if not self.project_key:
            raise Exception("TRACEX_PROJECT_KEY environment variable is required")
        self.invalid_key = "invalid_key_for_testing"

    @task(9)
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

        start_time = time.time()
        with self.client.post("/api/v1/crashes", json=payload, headers=headers, catch_response=True) as response:
            server_latency = None
            if "x-response-time-ms" in response.headers:
                server_latency = float(response.headers["x-response-time-ms"])

            if response.status_code == 202:
                response.success()
                if server_latency is not None:
                    self.environment.events.request.fire(
                        request_type="SERVER_LATENCY",
                        name="HTTP 202 Accepted",
                        response_time=server_latency,
                        response_length=len(response.content) if response.content else 0,
                        exception=None,
                        context=None
                    )
            elif response.status_code == 429:
                if "Retry-After" in response.headers:
                    response.success()
                    self.environment.events.request.fire(
                        request_type="SERVER_LATENCY",
                        name="HTTP 429 Rate Limited",
                        response_time=server_latency if server_latency else 0,
                        response_length=len(response.content) if response.content else 0,
                        exception=None,
                        context=None
                    )
                else:
                    response.failure("429 missing Retry-After header")
            else:
                response.failure(f"Unexpected status code {response.status_code}")

    @task(1)
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

        with self.client.post("/api/v1/crashes", json=payload, headers=headers, catch_response=True) as response:
            if response.status_code in [400, 401, 403]:
                response.success()
            elif response.status_code == 429:
                response.success()
            else:
                response.failure(f"Expected auth failure, got {response.status_code}")
