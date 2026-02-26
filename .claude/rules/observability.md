---
description: Standards for logging, monitoring, and debugging
paths:
  - "src/**"
---

# Observability & Logging

Since the Orchestrator runs in the background or mediates traffic, visibility into its health is mandatory.

## 1. General Principles
- **No `console.log` in Production**: Use a dedicated logging library (e.g., Winston, Pino) configured to output standard JSON. This ensures logs are easily parseable by aggregators (ELK, Datadog).
- **Log Levels**: Use appropriate levels:
  - `error`: System failures or unhandled exceptions that require immediate attention.
  - `warn`: Handled exceptions, deprecation warnings, or retry events.
  - `info`: Key operational milestones (e.g., server started, batch job completed).
  - `debug`: Detailed diagnostic information (disabled in production).

## 2. Execution Context
- **Correlation IDs**: Attach a unique request/trace ID to every incoming request and pass it down the call stack to external services.
- **Contextual Fields**: Include relevant metadata in log objects (e.g., `userId`, `tenantId`, `endpoint`) rather than concatenating them into the message string.

## 3. Health Checks and Metrics
- **Liveness/Readiness**: Expose endpoints (`/health`, `/metrics`) to allow load balancers or Kubernetes to monitor the service status.
- **Business Metrics**: Track non-technical metrics if relevant (e.g., jobs queued, jobs processed).
