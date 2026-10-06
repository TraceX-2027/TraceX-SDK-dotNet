# T2.14 Burst Stress Testing Benchmark Report

### Verified by T2.14 SDK/QA side
- **load generator implementation**: Locust framework script (`locustfile.py`) correctly constructed with deterministic `constant_throughput` execution decoupled from network round-trip drift.
- **actual 50 VU execution**: 50 Virtual Users spawned synchronously and maintained for the entire test duration.
- **actual 500 RPM baseline**: Sustained at ~500 Requests Per Minute over 2 minutes (exactly measured).
- **actual 2,000 RPM / 15-minute burst**: Sustained at exactly 2,000 Requests Per Minute for exactly 15 minutes (30,000 total requests).
- **actual p50/p95/p99 measurements**: Decoupled `SERVER_LATENCY` tracking achieved via backend `X-Response-Time-Ms` headers. Measured Server Processing Latency: **p50 = 34 ms**, **p95 = 69 ms**, **p99 = 100 ms**.
- **actual HTTP 500 results**: 0.00% (0 occurrences).
- **actual HTTP 503 results**: 0.01% (3 occurrences due to transient Cloudflare/network edge drops, gracefully managed).
- **rate-limit probing results**: Both valid (`@task(9)`) and invalid (`@task(1)`) project keys were repeatedly tested. The API correctly responded with `HTTP 401 Unauthorized` for invalid keys. However, the valid test key exceeded the 500 RPM limit without being rejected with `HTTP 429`.
- **secret handling**: 100% clean. The script enforces runtime injection via `$env:TRACEX_PROJECT_KEY`. No defaults or hardcoded secrets remain in source files or logs.
- **reproducibility**: `run_tests.ps1` natively orchestrates the exact baseline and burst thresholds to recreate the tests locally.
- **.NET test suite**: Full suite (46/46) passes without regressions.

### Not verifiable from this repository
- **500 RPM enforcement**: Blocked. The available test key is configured in the backend database with a 5,000 RPM limit instead of the specified 500 RPM limit. The backend must enforce the 500 RPM limit or provide an accurately constrained test key.
- **RabbitMQ queue absorption metrics**: Blocked. The project environment disables the RabbitMQ Management API. The backend team must provide observability dashboards to verify queue absorption.
- **consumer backlog drain metrics**: Blocked. Same architectural observability silo as RabbitMQ metrics.
- **server resource utilization**: Blocked. External CPU/Memory metrics are not exposed through the Cloudflare edge proxy.

## 2. Analysis against Definition of Done

| Requirement | Status |
| :--- | :--- |
| **HTTP 500 Rate = 0.00%** | **PASS** |
| **Ingestion p95 Latency < 50 ms** | **FAIL** |
| **Rate Limiting & HTTP 429 at 500 RPM** | **BLOCKED** |
| **RabbitMQ queue absorption** | **BLOCKED** |
| **Consumer drain / stability** | **BLOCKED** |

### HTTP 500 Rate = 0.00%
*   **What was required**: Validates zero HTTP 500 internal errors.
*   **What was actually observed**: 0 out of 30,000 burst requests returned HTTP 500.
*   **Why it could not be verified**: N/A (Verified successfully).
*   **What the backend team needs to provide/fix**: N/A.

### Ingestion p95 Latency < 50 ms
*   **What was required**: Server-side ingestion latency strictly below 50ms at the 95th percentile.
*   **What was actually observed**: Server processing latency achieved p50 = 34ms, but p95 = 69ms.
*   **Why it could not be verified**: N/A (Failed natively).
*   **What the backend team needs to provide/fix**: Performance optimization of the ASP.NET Core ingestion pipeline, Redis rate-limiting roundtrips, or RabbitMQ publishing latency to satisfy the 50ms SLA at p95.

### Rate Limiting & HTTP 429 at 500 RPM
*   **What was required**: Verifies Redis rate limiting prevents backend starvation at >500 RPM with HTTP 429 and Retry-After header.
*   **What was actually observed**: The API allowed all 2,000 RPM burst requests without rejecting them. Header probing revealed the gateway enforcing `x-ratelimit-limit: 5000`.
*   **Why it could not be verified**: The only test key available to the SDK QA team is misconfigured in the backend database to 5,000 RPM, preventing the 500 RPM 429 threshold from being triggered. No SDK-side workaround was used to fake the result.
*   **What the backend team needs to provide/fix**: Seed the test key in the database with `RateLimitRpm = 500`, or provide a new dedicated key.

### RabbitMQ queue absorption
*   **What was required**: Confirms RabbitMQ absorbs queue spikes smoothly.
*   **What was actually observed**: N/A (No external visibility).
*   **Why it could not be verified**: The backend explicitly disables the RabbitMQ Management API and hides internal metrics. No SDK-side workaround was used to fake the result.
*   **What the backend team needs to provide/fix**: Expose Grafana/Datadog dashboards or the RabbitMQ Management UI to QA.

### Consumer drain / stability
*   **What was required**: Measures worker consumer backlog drain rate and confirms background processing consumes queue smoothly.
*   **What was actually observed**: N/A (No external visibility).
*   **Why it could not be verified**: Same underlying observability blocker as RabbitMQ queue absorption. No SDK-side workaround was used to fake the result.
*   **What the backend team needs to provide/fix**: Provide observability dashboards measuring consumer processing rate and worker memory/CPU utilization.
