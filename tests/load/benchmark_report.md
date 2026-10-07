# T2.14 Burst Stress Testing Benchmark Report

### Verified by T2.14 SDK/QA side
- **Load Generator Implementation**: Locust framework script (`locustfile.py`) corrected to decouple valid crash tests from invalid key validation. Custom test stop listeners now compute server-side latency using `x-response-time-ms` without distorting `request.fire()` counts.
- **Error Handling & Fast Failure**: Native runner scripts (`run_tests.ps1`, `run_tests.sh`) updated to abort tests early on failure (`$LASTEXITCODE` checks).
- **Environment & Secrets Handling**: 100% clean. The script enforces runtime injection via `$env:TRACEX_PROJECT_KEY`. Added `.gitignore` exclusions for volatile CSV output to maintain Git repository integrity.
- **Invalid Key Validation**: Script accurately asserts `HTTP 401 Unauthorized` for invalid project keys.
- **Backend Configuration Findings**: Code inspection of the backend confirms that newly generated keys default to a rate limit of `RateLimitRpm = 500`. Furthermore, the backend properly implements HTTP 429 and `Retry-After` headers, and exposes `X-Response-Time-Ms` and `Server-Timing` headers.

### BLOCKED Dependencies (Execution Halted)

1. **Valid Test Project API Key Missing**: 
   The load test could not be executed because the previously used key (`tracex_live_pub_98a72bc4`) has been revoked and returns `HTTP 401 Unauthorized`. A newly generated, valid test key with a 500 RPM rate limit is required to run the main 2,000 RPM burst benchmark.
2. **RabbitMQ Observability Credentials Missing**: 
   The project environment has `ManagementApiEnabled: false` locally, and the external RabbitMQ management URL/credentials provided by the backend team were not found in the accessible environment or documentation.
3. **Consumer Drain & Stability Metrics**: 
   Blocked due to the same lack of observability access as RabbitMQ. Memory/CPU metrics of the consumer workers cannot be independently verified.

## 2. Analysis against Definition of Done

| Requirement | Status |
| :--- | :--- |
| **HTTP 500 Rate = 0.00%** | **BLOCKED** (Needs valid key) |
| **Ingestion p95 Latency < 75 ms (preferred <= 70 ms)** | **BLOCKED** (Needs valid key) |
| **Rate Limiting & HTTP 429 at 500 RPM** | **BLOCKED** (Needs valid key) |
| **RabbitMQ queue absorption** | **BLOCKED** (Needs observability access) |
| **Consumer drain / stability** | **BLOCKED** (Needs observability access) |

*(Note: Old, distorted results from previous test runs using the revoked 5,000 RPM key are explicitly superseded and excluded from this report.)*

### Current Execution Results

#### Baseline & Burst Metrics
- **Actual Achieved RPM**: N/A (Blocked)
- **Total Valid Requests**: N/A (Blocked)
- **Server Processing Latency (p50 / p95 / p99)**: N/A (Blocked)
- **HTTP 500 Rate**: N/A (Blocked)

#### Rate Limit (500 RPM)
- **HTTP 429 Count / Retry-After Behavior**: N/A (Blocked). We have verified via code inspection that the backend *should* return 429 and Retry-After, but live execution is pending a valid test key.

#### Infrastructure Observability (RabbitMQ & Consumers)
- **Queue Depth / Ready / Unacked**: N/A (Blocked)
- **Backlog Growth & Drain**: N/A (Blocked)
- **Consumer Processing Failures**: N/A (Blocked)
